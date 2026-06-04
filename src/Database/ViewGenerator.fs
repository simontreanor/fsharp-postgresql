/// View generator. Translates F# quotation-based view definitions into SQL CREATE VIEW
/// statements. The quotation expression tree is traversed to derive JOIN structure,
/// field mappings, and filter conditions — catching field-reference mistakes at compile time.
module Database.ViewGenerator
#if !FABLE_COMPILER

open System

open FSharp.Quotations
open FSharp.Quotations.Patterns

open Database.LocalViews
open Shared.Grammar
open Shared.Config

open Database.PostgreSQL

let generateViewSql (viewName: string) (baseTable: string) (fields: (string * string) array) = [|
    $"CREATE VIEW {SCHEMA_NAME}.{viewName} WITH (security_invoker = ON) AS"
    "    SELECT"
    fields
    |> Array.map(fun (name, expr) -> $"        {expr} AS {name}")
    |> String.concat ",\n    "
    $"    FROM {SCHEMA_NAME}.{baseTable};"
|]

let translateExpr(expr: Expr) =
    // Reduce pipe / application / let nodes to normalised form
    let rec reduce expr =
        match expr with
        | Call(None, mi, [ left; right ]) when mi.Name = "op_PipeRight" -> reduce(Expr.Application(right, left))
        | Application(func, arg) ->
            match func with
            | Lambda(param, body) ->
                let substituted = body.Substitute(fun v -> if v = param then Some arg else None)
                reduce substituted
            | Let(var, value, body) ->
                match body with
                | Lambda(param, innerBody) ->
                    let substitutedInner = innerBody.Substitute(fun v -> if v = param then Some arg else None)
                    let substituted = substitutedInner.Substitute(fun v -> if v = var then Some value else None)
                    reduce substituted
                | _ -> failwith "Unsupported let in application during reduction"
            | _ -> expr
        | Let(var, value, body) ->
            let substituted = body.Substitute(fun v -> if v = var then Some value else None)
            reduce substituted
        | _ -> expr

    let rec translateExprInner expr =
        let expr = reduce expr

        match expr with
        | PropertyGet(Some(Var v), pi, []) -> $"{v.Name}.{toSnakeCase pi.Name}"
        | PropertyGet(Some(Call(None, mi, [ Lambda(findParam, findBody); arrayExpr ])), pi, []) when mi.Name = "Find" ->
            match arrayExpr with
            | PropertyGet(None, arrPi, []) ->
                let elementType =
                    if arrPi.PropertyType.IsArray then
                        arrPi.PropertyType.GetElementType()
                    elif arrPi.PropertyType.IsGenericType then
                        arrPi.PropertyType.GenericTypeArguments[0]
                    else
                        failwith "Unsupported collection type for Find"

                let table = getTableName elementType
                let col = toSnakeCase pi.Name
                $"(SELECT {findParam.Name}.{col} FROM {SCHEMA_NAME}.{table} {findParam.Name} WHERE {translateExprInner findBody} LIMIT 1)"
            | _ -> failwith "Unsupported array expression for Find"
        | Var v -> v.Name
        | Call(None, mi, [ left; right ]) when mi.Name = "op_Equality" -> $"({translateExprInner left} = {translateExprInner right})"
        | Call(None, mi, [ left; right ]) when mi.Name = "op_BooleanAnd" -> $"({translateExprInner left} AND {translateExprInner right})"
        | Call(None, mi, [ left; right ]) when mi.Name = "op_BooleanOr" -> $"({translateExprInner left} OR {translateExprInner right})"
        | Call(None, mi, [ predicate; array ]) when mi.Name = "Exists" ->
            match predicate, array with
            | Lambda(param, body), PropertyGet(None, pi, []) ->
                let elementType =
                    if pi.PropertyType.IsArray then
                        pi.PropertyType.GetElementType()
                    elif pi.PropertyType.IsGenericType then
                        pi.PropertyType.GenericTypeArguments[0]
                    else
                        failwith "Unsupported collection type for Exists"

                let table = getTableName elementType
                $"EXISTS (SELECT 1 FROM {SCHEMA_NAME}.{table} {param.Name} WHERE {translateExprInner body})"
            | _ -> failwith "Unsupported Exists call"
        | Call(None, mi, [ left; right ]) when mi.Name = "op_PipeRight" -> translateExprInner(Expr.Application(right, left))
        | Application(func, arg) ->
            match func with
            | Lambda(param, body) ->
                let substituted = body.Substitute(fun v -> if v = param then Some arg else None)
                translateExprInner substituted
            | Let(var, value, body) ->
                match body with
                | Lambda(param, innerBody) ->
                    let substitutedInner = innerBody.Substitute(fun v -> if v = param then Some arg else None)
                    let substituted = substitutedInner.Substitute(fun v -> if v = var then Some value else None)
                    translateExprInner substituted
                | _ -> failwith "Unsupported let in application"
            | _ -> failwith "Unsupported application"
        | Let(var, value, body) ->
            let substituted = body.Substitute(fun v -> if v = var then Some value else None)
            translateExprInner substituted
        | IfThenElse(condition, trueBranch, falseBranch) ->
            match falseBranch with
            | Value(v, _) when v :? bool && (v :?> bool) = false ->
                $"({translateExprInner condition} AND {translateExprInner trueBranch})"
            | _ ->
                $"CASE WHEN {translateExprInner condition} THEN {translateExprInner trueBranch} ELSE {translateExprInner falseBranch} END"
        | Value(v, _) ->
            match v with
            | :? bool as b -> if b then "TRUE" else "FALSE"
            | _ -> string v
        | _ -> failwith $"Unsupported expression: {expr}"

    translateExprInner expr

/// Generate a JOIN-based view SQL for one-to-many mappings
let generateJoinViewSql (viewName: string) (fromClause: string) (fields: (string * string) array) (joins: string array) = [|
    $"CREATE VIEW {SCHEMA_NAME}.{viewName} WITH (security_invoker = ON) AS"
    "    SELECT"
    fields
    |> Array.map(fun (name, expr) -> $"        {expr} AS {name}")
    |> String.concat ",\n    "
    $"    FROM {SCHEMA_NAME}.{fromClause}"
    yield! joins |> Array.map(fun j -> $"    {j}")
    ";"
|]

/// Extract table name, alias, and predicate from an Array.filter expression
let extractFilterInfo(expr: Expr) =
    let rec reduce expr =
        match expr with
        | Call(None, mi, [ left; right ]) when mi.Name = "op_PipeRight" -> reduce(Expr.Application(right, left))
        | Application(func, arg) ->
            match func with
            | Lambda(param, body) ->
                let substituted = body.Substitute(fun v -> if v = param then Some arg else None)
                reduce substituted
            | Let(var, value, body) ->
                match body with
                | Lambda(param, innerBody) ->
                    let substitutedInner = innerBody.Substitute(fun v -> if v = param then Some arg else None)
                    let substituted = substitutedInner.Substitute(fun v -> if v = var then Some value else None)
                    reduce substituted
                | _ -> expr
            | _ -> expr
        | Let(var, value, body) ->
            let substituted = body.Substitute(fun v -> if v = var then Some value else None)
            reduce substituted
        | _ -> expr

    let expr = reduce expr

    match expr with
    | Call(None, mi, [ Lambda(filterParam, filterBody); arrayExpr ]) when mi.Name = "Filter" ->
        match arrayExpr with
        | PropertyGet(None, pi, []) ->
            let elementType =
                if pi.PropertyType.IsArray then
                    pi.PropertyType.GetElementType()
                elif pi.PropertyType.IsGenericType then
                    pi.PropertyType.GenericTypeArguments[0]
                else
                    failwith "Unsupported collection type"

            let tableName = getTableName elementType
            Some(tableName, filterParam.Name, filterBody)
        | _ -> None
    | _ -> None

/// Translate a filter predicate to a SQL JOIN condition
let translateJoinCondition(predBody: Expr) =
    let rec translate expr =
        match expr with
        | Call(None, mi, [ left; right ]) when mi.Name = "op_Equality" -> $"{translateExpr left} = {translateExpr right}"
        | Call(None, mi, [ left; right ]) when mi.Name = "op_BooleanAnd" -> $"{translate left} AND {translate right}"
        | _ -> translateExpr expr

    translate predBody

let generateViewFromQuotation viewName baseTable baseAlias (mapping: Expr<'a -> 'b>) =
    let rec reduce expr =
        match expr with
        | Let(var, value, body) ->
            let substituted = body.Substitute(fun v -> if v = var then Some(reduce value) else None)
            reduce substituted
        | Call(None, mi, [ left; right ]) when mi.Name = "op_PipeRight" -> reduce(Expr.Application(reduce right, reduce left))
        | Application(func, arg) ->
            let func = reduce func
            let arg = reduce arg

            match func with
            | Lambda(param, body) ->
                let substituted = body.Substitute(fun v -> if v = param then Some arg else None)
                reduce substituted
            | _ -> Expr.Application(func, arg)
        | Call(target, mi, args) ->
            let reducedArgs = args |> List.map reduce

            match target with
            | Some t -> Expr.Call(reduce t, mi, reducedArgs)
            | None -> Expr.Call(mi, reducedArgs)
        | NewRecord(recordType, fieldExprs) -> Expr.NewRecord(recordType, fieldExprs |> List.map reduce)
        | Lambda(param, body) -> Expr.Lambda(param, reduce body)
        | _ -> expr

    match mapping with
    | Lambda(param, body) ->
        let reducedBody = reduce body

        match reducedBody with
        // Direct record mapping — one-to-one
        | NewRecord(recordType, fieldExprs) ->
            let properties = recordType.GetProperties()

            let fields =
                Array.zip properties (List.toArray fieldExprs)
                |> Array.map(fun (pi, expr) ->
                    toSnakeCase pi.Name, translateExpr expr
                )

            generateViewSql viewName $"{baseTable} {baseAlias}" fields

        // Array.collect pattern — expressed as outer filter -> collect -> inner filter -> map
        | Call(None, mi, [ Lambda(collectParam, collectBody); outerFilterExpr ]) when mi.Name = "Collect" ->
            match extractFilterInfo outerFilterExpr with
            | Some(outerTable, outerAlias, outerPred) ->
                let reducedCollectBody = reduce collectBody

                match reducedCollectBody with
                | Call(None, mapMi, [ Lambda(mapParam, mapBody); innerFilterExpr ]) when mapMi.Name = "Map" ->
                    match extractFilterInfo innerFilterExpr with
                    | Some(innerTable, innerAlias, innerPred) ->
                        match mapBody with
                        | NewRecord(recordType, fieldExprs) ->
                            let properties = recordType.GetProperties()

                            let fields =
                                Array.zip properties (List.toArray fieldExprs)
                                |> Array.map(fun (pi, expr) ->
                                    toSnakeCase pi.Name, translateExpr expr
                                )

                            let outerJoinCond = translateJoinCondition outerPred
                            let innerJoinCond = translateJoinCondition innerPred

                            let joins = [|
                                $"JOIN {SCHEMA_NAME}.{outerTable} {outerAlias} ON {outerJoinCond}"
                                $"JOIN {SCHEMA_NAME}.{innerTable} {innerAlias} ON {innerJoinCond}"
                            |]

                            generateJoinViewSql viewName $"{baseTable} {baseAlias}" fields joins
                        | _ -> failwithf "Expected NewRecord in Array.map body, got: %s" (mapBody.ToString())
                    | None -> failwith "Could not extract inner filter info in Array.collect"
                | _ -> failwithf "Expected Array.map in Array.collect body, got: %s" (reducedCollectBody.ToString())
            | None -> failwith "Could not extract outer filter info in Array.collect"

        // Array.map pattern — single join
        | Call(None, mi, [ Lambda(mapParam, mapBody); filterExpr ]) when mi.Name = "Map" ->
            match extractFilterInfo filterExpr with
            | Some(joinTable, joinAlias, joinPred) ->
                let rec extractRecord expr =
                    let expr = reduce expr

                    match expr with
                    | NewRecord(recordType, fieldExprs) -> Some(recordType, fieldExprs)
                    | _ -> None

                match extractRecord mapBody with
                | Some(recordType, fieldExprs) ->
                    let properties = recordType.GetProperties()

                    let fields =
                        Array.zip properties (List.toArray fieldExprs)
                        |> Array.map(fun (pi, expr) ->
                            toSnakeCase pi.Name, translateExpr expr
                        )

                    let joinCond = translateJoinCondition joinPred
                    let joins = [| $"JOIN {SCHEMA_NAME}.{joinTable} {joinAlias} ON {joinCond}" |]
                    generateJoinViewSql viewName $"{baseTable} {baseAlias}" fields joins
                | None -> failwithf "Could not extract record from Array.map body: %s" (mapBody.ToString())
            | None -> failwith "Could not extract filter info in Array.map"

        | _ -> failwithf "Mapping must return a record or array pattern. Body: %s" (reducedBody.ToString())
    | _ -> failwith "Mapping must be a lambda"

let generateAllViews() =
    [|
        $"-- generated views ({DateTimeOffset.UtcNow:``yyyy-MM-dd HH:mm:ss``} UTC)"
        ""
        "-- booking_details: 4-table join (bookings → partners, customers, services, resources)"
        yield! generateViewFromQuotation "booking_details" "bookings" "b" bookingDetailView
        ""
        "-- service_availability: service-resource join via service_resources"
        yield! generateViewFromQuotation "service_availability" "service_resources" "sr" serviceAvailabilityView
        ""
        "-- invoice_details: 2-table join (invoices → bookings)"
        yield! generateViewFromQuotation "invoice_details" "invoices" "inv" invoiceDetailView
        ""
        "-- end of generated views"
    |]
    |> String.concat "\n"

#endif
