module Database.PostgreSQL

open System
open System.Reflection

open Shared
open Shared.Attributes
open Shared.Grammar
open Shared.Config

open Microsoft.FSharp.Reflection

#if !FABLE_COMPILER

/// Check if type is an F# option type
let isOptionType(t: Type) = t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Option<_>>

let getUnderlyingOptionType(t: Type) = if isOptionType t then t.GetGenericArguments()[0] else t

let isArrayType(t: Type) = t.IsArray

/// Check if a type is a single-case discriminated union
let isSingleCaseUnion(t: Type) = FSharpType.IsUnion t && FSharpType.GetUnionCases(t).Length = 1

/// Extract the wrapped value from a single-case union
let unwrapSingleCaseUnion(value: obj) =
    if isNull value then
        null
    else
        let t = value.GetType()

        if isSingleCaseUnion t then
            let case, fields = FSharpValue.GetUnionFields(value, t)
            if fields.Length > 0 then fields[0] else null
        else
            value

let getUnionCaseType(t: Type) =
    if isSingleCaseUnion t then
        let cases = FSharpType.GetUnionCases t
        let fields = cases[0].GetFields()

        if fields.Length > 0 then
            Some fields[0].PropertyType
        else
            None
    else
        None

/// Get the snake_case value for a DU case (used when serialising enum values to SQL literals)
let getUnionCaseValue convertCase (value: obj) =
    if isNull value then
        null
    else
        let t = value.GetType()
        let case, _ = FSharpValue.GetUnionFields(value, t)
        convertCase case.Name

/// Check if type is an F# union (enum-like, more than one case)
let isEnumType(t: Type) = FSharpType.IsUnion t && not <| isSingleCaseUnion t

let getEnumCases(t: Type) =
    if isEnumType t then
        FSharpType.GetUnionCases t |> Array.map _.Name
    else
        [||]

/// Unwrap an option value (returns the inner value or null)
let unwrapOption(value: obj) =
    if isNull value then
        null
    else
        let t = value.GetType()

        if isOptionType t then
            let case, fields = FSharpValue.GetUnionFields(value, t)

            if case.Name = "Some" && fields.Length > 0 then
                fields[0]
            else
                null
        else
            value

/// Check if type is an anonymous record
let isAnonymousRecord(t: Type) =
    t.FullName <> null
    && (t.FullName.Contains "<>f__AnonymousType"
        || t.Name.StartsWith "FSharpAnonRecdType"
        || t.GetCustomAttributes false
           |> Array.exists(fun attr -> attr.GetType().Name.Contains "CompilationMapping"))

/// Map F# types to PostgreSQL column types
let rec fsharpTypeToPostgres(t: Type) : string =
    let actualType = if isOptionType t then getUnderlyingOptionType t else t

    if isSingleCaseUnion actualType then
        match getUnionCaseType actualType with
        | Some innerType -> fsharpTypeToPostgres innerType
        | None -> "text"
    else
        match actualType with
        | t when t = typeof<Guid> -> "uuid"
        | t when t = typeof<string> -> "text"
        | t when t = typeof<int> -> "int4"
        | t when t = typeof<int64> -> "int8"
        | t when t = typeof<decimal> -> "numeric"
        | t when t = typeof<float> -> "float8"
        | t when t = typeof<bool> -> "bool"
        | t when t = typeof<DateTime> -> "timestamp"
        | t when t = typeof<DateTimeOffset> -> "timestamptz"
        | t when t = typeof<TimeSpan> -> "time"
        | t when isArrayType t ->
            let elementType = t.GetElementType()

            if isEnumType elementType then
                let enumTypeName = toSnakeCase elementType.Name
                $"{SCHEMA_NAME}.{enumTypeName}[]"
            else
                let pgType = fsharpTypeToPostgres elementType
                $"{pgType}[]"
        | t when isAnonymousRecord t -> "jsonb"
        | t when isEnumType t ->
            let enumTypeName = toSnakeCase actualType.Name
            $"{SCHEMA_NAME}.{enumTypeName}"
        | _ -> "text"

let escapeString(s: string) = s.Replace("'", "''")

/// Escape a string value for safe embedding inside a JSON literal
let private escapeJsonString(s: string) =
    s.Replace("\\", "\\\\")
     .Replace("\"", "\\\"")
     .Replace("\n", "\\n")
     .Replace("\r", "\\r")
     .Replace("\t", "\\t")

/// Serialize anonymous record to JSON
let rec serializeAnonymousRecord(value: obj) : string =
    if isNull value then
        "null"
    else
        let t = value.GetType()
        let properties = t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)

        let fields =
            properties
            |> Array.map(fun p ->
                let name = toSnakeCase p.Name
                let value = p.GetValue(value)
                let jsonValue = serializeValue value
                $"\"{name}\": {jsonValue}"
            )
            |> String.concat ", "

        $"{{{fields}}}"

and serializeValue (value: obj) : string =
    match value with
    | null -> "null"
    | :? string as s -> $"\"{escapeJsonString s}\""
    | :? bool as b -> if b then "true" else "false"
    | :? int as i -> i.ToString()
    | :? decimal as d -> d.ToString("G", Globalization.CultureInfo.InvariantCulture)
    | _ when isOptionType(value.GetType()) ->
        let inner = unwrapOption value
        serializeValue inner
    | _ when value.GetType().IsArray ->
        let arr = value :?> Array
        let items = [| for i in 0 .. arr.Length - 1 -> serializeValue (arr.GetValue i) |]
        let joined = String.concat ", " items
        $"[{joined}]"
    | _ when isAnonymousRecord(value.GetType()) -> serializeAnonymousRecord value
    | _ -> $"\"{escapeJsonString(value.ToString())}\""

#endif

let inline getEnumName(enum: Type) = toSnakeCase enum.Name

let inline getTableName(table: Type) =
    let typeName =
        if table.Name.Contains "`" then
            table.Name.Substring(0, table.Name.IndexOf '`')
        else
            table.Name

    typeName |> toSnakeCase |> pluralise

let inline getColumnName(prop: PropertyInfo) = toSnakeCase prop.Name

let inline getForeignKeyTableName(fkAttr: FKAttribute) = getTableName fkAttr.TableType

#if !FABLE_COMPILER

module Generator =

    /// Discover all DU types in the Enums module via reflection on a known anchor type
    let getEnums() =
        typeof<Shared.Schema.Enums.BookingStatus>.DeclaringType.GetNestedTypes(BindingFlags.Public ||| BindingFlags.NonPublic)

    /// Discover all record types in the Tables module via reflection on a known anchor type
    let getTables() =
        typeof<Shared.Schema.Tables.Tenant>.DeclaringType.GetNestedTypes(BindingFlags.Public ||| BindingFlags.NonPublic)

    /// Discover all record types in the Views module via reflection on a known anchor type
    let getViews() =
        typeof<Shared.Schema.Views.BookingDetail>.DeclaringType.GetNestedTypes(BindingFlags.Public ||| BindingFlags.NonPublic)

    let createSchema() = [|
        $"DROP SCHEMA IF EXISTS {SCHEMA_NAME} CASCADE;"
        $"CREATE SCHEMA {SCHEMA_NAME};"
        $"GRANT USAGE ON SCHEMA {SCHEMA_NAME} TO authenticated, service_role;"
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {SCHEMA_NAME} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO authenticated;"
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {SCHEMA_NAME} GRANT ALL ON TABLES TO service_role;"
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {SCHEMA_NAME} GRANT USAGE, SELECT ON SEQUENCES TO authenticated, service_role;"
    |]

    let createEnumTypes() =
        getEnums()
        |> Array.map(fun enum ->
            let enumName = getEnumName enum
            let cases = FSharpType.GetUnionCases enum

            let casesList =
                cases
                |> Array.map(fun case -> $"'{toSnakeCase case.Name}'")
                |> String.concat ", "

            $"CREATE TYPE {SCHEMA_NAME}.{enumName} AS ENUM ({casesList});\n"
        )

    let hasAttribute<'T when 'T :> Attribute>(element: ICustomAttributeProvider) =
        element.GetCustomAttributes(typeof<'T>, false).Length > 0

    let getAttribute<'T when 'T :> Attribute>(element: ICustomAttributeProvider) =
        let attrs = element.GetCustomAttributes(typeof<'T>, false)
        if attrs.Length > 0 then Some(attrs[0] :?> 'T) else None

    let getAttributes<'T when 'T :> Attribute>(element: ICustomAttributeProvider) =
        element.GetCustomAttributes(typeof<'T>, false)
        |> Array.map(fun attr -> attr :?> 'T)

    let getPKAttribute(prop: PropertyInfo) = getAttribute<PKAttribute> prop

    let getUniqueAttributes(prop: PropertyInfo) = getAttributes<UniqueAttribute> prop

    let getDefaultAttribute(prop: PropertyInfo) = getAttribute<DefaultAttribute> prop

    let getFKAttributes(prop: PropertyInfo) = getAttributes<FKAttribute> prop

    type ForeignKeyDefinition = { TableName: string; Prefix: string }

    type ColumnDefinition = {
        Name: string
        Type: string
        IsNullable: bool
        IsPrimaryKey: bool
        UniqueGroups: string array
        DefaultValue: string option
        ForeignKeyTables: ForeignKeyDefinition array
        IsWithoutOverlap: bool
    }

    type TableDefinition = { TableName: string; Columns: ColumnDefinition array }

    let createColumns(prop: PropertyInfo) : ColumnDefinition =
        let propType = prop.PropertyType
        let isNullable = isOptionType propType

        let actualType =
            if isNullable then
                getUnderlyingOptionType propType
            else
                propType

        let isPK = hasAttribute<PKAttribute> prop
        let uniqueAttrs = getUniqueAttributes prop
        let uniqueGroups = uniqueAttrs |> Array.map _.Group
        let defaultAttr = getDefaultAttribute prop
        let fkAttrs = getFKAttributes prop

        let fkTables =
            fkAttrs
            |> Array.map(fun a -> { TableName = getTableName a.TableType; Prefix = a.Prefix })

        let isWithoutOverlap = hasAttribute<WithoutOverlapAttribute> prop

        let pgType =
            if isEnumType actualType then
                $"{SCHEMA_NAME}.{toSnakeCase actualType.Name}"
            else
                fsharpTypeToPostgres actualType

        {
            Name = getColumnName prop
            Type = pgType
            IsNullable = isNullable && not isPK
            IsPrimaryKey = isPK
            UniqueGroups = uniqueGroups
            DefaultValue = defaultAttr |> Option.map _.DefaultValue
            ForeignKeyTables = fkTables
            IsWithoutOverlap = isWithoutOverlap
        }

    let getTableDefinitions() =
        getTables()
        |> Array.map(fun table ->
            let tableName = getTableName table

            table.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
            |> Array.map createColumns
            |> fun columns -> { TableName = tableName; Columns = columns }
        )

    let createColumn(column: ColumnDefinition) =
        let columnType =
            match column.Type with
            | "int8" when column.IsPrimaryKey -> "bigserial"
            | other -> other

        let nullable = if column.IsNullable then "" else "NOT NULL"

        let defaultValue =
            match column.DefaultValue with
            | Some defaultVal -> $"DEFAULT {defaultVal}"
            | _ ->
                if
                    column.IsPrimaryKey
                    && columnType = "uuid"
                    && column.ForeignKeyTables |> Array.isEmpty
                then
                    "DEFAULT gen_random_uuid()"
                elif
                    not column.IsNullable
                    && columnType = "timestamptz"
                    && column.Name.Contains "created"
                then
                    "DEFAULT now()"
                else
                    ""

        [| column.Name; columnType; nullable; defaultValue |]
        |> Array.filter((<>) "")
        |> String.concat " "
        |> fun s -> $"    {s}"

    let createTable tableDefinition = [|
        $"CREATE TABLE {SCHEMA_NAME}.{tableDefinition.TableName} ("
        tableDefinition.Columns |> Array.map createColumn |> String.concat ",\n"
        ");\n"
    |]

    let createPrimaryKeys tableDefinition =
        let pkColumns =
            tableDefinition.Columns
            |> Array.filter _.IsPrimaryKey
            |> Array.map _.Name

        if pkColumns |> Array.isEmpty then
            None
        else
            Some $"""ALTER TABLE {SCHEMA_NAME}.{tableDefinition.TableName} ADD CONSTRAINT {tableDefinition.TableName}_pkey PRIMARY KEY ({pkColumns |> String.concat ", "});"""

    let enableRls tableDefinition = [|
        $"ALTER TABLE {SCHEMA_NAME}.{tableDefinition.TableName} ENABLE ROW LEVEL SECURITY;"
        $"ALTER TABLE {SCHEMA_NAME}.{tableDefinition.TableName} FORCE ROW LEVEL SECURITY;\n"
    |]

    let createUniqueConstraints tableDefinition =
        let uniqueColumns =
            tableDefinition.Columns
            |> Array.filter(fun column -> column.UniqueGroups |> Array.isEmpty |> not)

        if uniqueColumns |> Array.isEmpty then
            [||]
        else
            uniqueColumns
            |> Array.collect(fun uniqueColumn -> uniqueColumn.UniqueGroups |> Array.map(fun group -> group, uniqueColumn.Name))
            |> Array.groupBy fst
            |> Array.map(fun (group, columns) -> group, columns |> Array.map snd)
            |> Array.collect(fun (constraintName, columnNames) -> [|
                $"""CREATE UNIQUE INDEX {constraintName} ON {SCHEMA_NAME}.{tableDefinition.TableName} ({columnNames |> String.concat ", "}) NULLS NOT DISTINCT;"""
                $"ALTER TABLE {SCHEMA_NAME}.{tableDefinition.TableName} ADD CONSTRAINT {constraintName} UNIQUE USING INDEX {constraintName};\n"
            |])

    let validateAndCreateForeignKeys(tableDefinitions: TableDefinition array) =
        // Collect unique column sets for each table
        let uniqueColumnSets =
            tableDefinitions
            |> Array.map(fun tableDef ->
                let pkColumns =
                    tableDef.Columns
                    |> Array.filter _.IsPrimaryKey
                    |> Array.map _.Name
                    |> Set.ofArray

                let uniqueGroups =
                    tableDef.Columns
                    |> Array.collect _.UniqueGroups
                    |> Array.distinct
                    |> Array.map(fun group ->
                        tableDef.Columns
                        |> Array.filter(fun col -> col.UniqueGroups |> Array.contains group)
                        |> Array.map _.Name
                        |> Set.ofArray
                    )

                let allUniqueSets = pkColumns :: Array.toList uniqueGroups

                tableDef.TableName, allUniqueSets
            )
            |> Map.ofArray

        // Validate and generate FK constraints
        tableDefinitions
        |> Array.collect(fun tableDefinition ->
            let fkColumns =
                tableDefinition.Columns
                |> Array.filter(fun col -> col.ForeignKeyTables.Length > 0)

            fkColumns
            |> Array.collect(fun col -> col.ForeignKeyTables |> Array.map(fun tp -> tp, col.Name))
            |> Array.groupBy fst
            |> Array.map(fun (fkd, cols) -> fkd.TableName, fkd.Prefix, cols |> Array.map snd)
            |> Array.choose(fun (fkTable, prefix, fkCols) ->
                let thoseCols =
                    fkCols
                    |> Array.map(fun col ->
                        if prefix.Length > 0 && col.StartsWith prefix then
                            col[prefix.Length ..]
                        else
                            col
                    )

                let thatColSet = Set.ofArray thoseCols

                match Map.tryFind fkTable uniqueColumnSets with
                | Some uniqueSets ->
                    if not(List.exists ((=) thatColSet) uniqueSets) then
                        let fkColsStr = String.Join(", ", fkCols)
                        let thatColsStr = String.Join(", ", thoseCols)

                        let available =
                            uniqueSets
                            |> List.map(fun s -> String.Join(", ", Set.toList s))
                            |> fun xs -> String.Join("; ", xs)

                        failwith $"Foreign key from {tableDefinition.TableName}({fkColsStr}) to {fkTable}({thatColsStr}) references columns that are not unique. Available unique sets: {available}"
                    else
                        let constraintName =
                            [|
                                yield tableDefinition.TableName
                                if not(String.IsNullOrEmpty prefix) then
                                    yield prefix
                                yield fkTable
                                yield "fkey"
                            |]
                            |> String.concat "_"

                        let thisColList = String.Join(", ", fkCols)

                        let thatColList =
                            fkCols
                            |> Array.map(fun col ->
                                if prefix.Length > 0 && col.StartsWith prefix then
                                    col[prefix.Length ..]
                                else
                                    col
                            )
                            |> String.concat ", "

                        Some $"ALTER TABLE {SCHEMA_NAME}.{tableDefinition.TableName} ADD CONSTRAINT {constraintName} FOREIGN KEY ({thisColList}) REFERENCES {SCHEMA_NAME}.{fkTable}({thatColList});"
                | None ->
                    failwith $"Foreign key references unknown table '{fkTable}' from {tableDefinition.TableName}"
            )
        )

    let createWithoutOverlapConstraints tableDefinition =
        let isWithoutOverlap = tableDefinition.Columns |> Array.exists _.IsWithoutOverlap

        if not isWithoutOverlap then
            None
        else
            let equalityColumns, rangeColumns =
                tableDefinition.Columns
                |> Array.filter _.IsWithoutOverlap
                |> Array.partition(fun col -> col.Type <> "timestamptz")
                |> fun (ecc, rcc) -> ecc |> Array.map _.Name, rcc |> Array.map _.Name

            let equalityCondition =
                equalityColumns
                |> Array.map(fun col -> $"{col} WITH =")
                |> String.concat ", "

            let rangeCondition = $"""tstzrange({String.Join(", ", rangeColumns)}) with &&"""

            let conditions =
                [
                    if not <| String.IsNullOrEmpty equalityCondition then
                        yield equalityCondition
                    if not <| String.IsNullOrEmpty rangeCondition then
                        yield rangeCondition
                ]
                |> String.concat ", "

            Some $"ALTER TABLE {SCHEMA_NAME}.{tableDefinition.TableName} ADD CONSTRAINT {tableDefinition.TableName}_without_overlaps EXCLUDE USING gist ({conditions});"

    let generateSchemaSql() =
        let tableDefinitions = getTableDefinitions()

        [|
            yield $"-- generated PostgreSQL schema ({DateTimeOffset.UtcNow:``yyyy-MM-dd HH:mm:ss``} UTC)"
            yield "\n-- schema"
            yield! createSchema()
            yield "\n-- extensions"
            yield $"CREATE EXTENSION IF NOT EXISTS btree_gist SCHEMA extensions;"
            yield "\n-- enum types"
            yield! createEnumTypes()
            yield "\n-- tables"
            yield! tableDefinitions |> Array.collect createTable
            yield "-- primary keys"
            yield! tableDefinitions |> Array.choose createPrimaryKeys
            yield "-- RLS"
            yield! tableDefinitions |> Array.collect enableRls
            yield "-- unique constraints"
            yield! tableDefinitions |> Array.collect createUniqueConstraints
            yield "-- foreign keys"
            yield! tableDefinitions |> validateAndCreateForeignKeys
            yield "-- without-overlap constraints"
            yield! tableDefinitions |> Array.choose createWithoutOverlapConstraints
            yield "\n-- end of schema"
        |]
        |> String.concat "\n"

#endif
