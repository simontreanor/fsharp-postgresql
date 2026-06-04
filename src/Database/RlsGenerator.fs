/// RLS policy generator. Derives row-level security policies from table topology:
/// which columns are present (tenant_id, partner_id, customer_id) determines
/// which scope kinds are generated for each table.
module Database.RlsGenerator
#if !FABLE_COMPILER

open System

open Shared.Config
open Database.PostgreSQL.Generator

[<RequireQualifiedAccess>]
type RlsScope =
    | Authenticated   // any authenticated user may SELECT
    | TenantOwn       // tenant_id in JWT claim must match
    | CustomerOwn     // customer_id must match current user
    | PartnerOwn      // partner_id must be one the user belongs to

/// Tables where any authenticated user (including pre-login flows) needs SELECT.
/// Extend this set for tables that must be readable before authentication.
let private authenticatedReadTables : Set<string> = Set.empty

/// Derive RLS scopes from each table's column topology
let rlsScopes =
    getTableDefinitions()
    |> Array.map(fun td ->
        [|
            if td.Columns |> Array.exists(fun col -> col.Name = "tenant_id") then
                yield RlsScope.TenantOwn
            if td.Columns |> Array.exists(fun col -> col.Name = "customer_id") then
                yield RlsScope.CustomerOwn
            if td.Columns |> Array.exists(fun col -> col.Name = "partner_id") then
                yield RlsScope.PartnerOwn
        |]
        |> fun scopes ->
            td.TableName,
            match scopes with
            | [||] -> [| RlsScope.Authenticated |]
            | [| scope |] when scope = RlsScope.TenantOwn -> [| RlsScope.Authenticated; scope |]
            | _ ->
                if Set.contains td.TableName authenticatedReadTables then
                    Array.append [| RlsScope.Authenticated |] scopes
                else
                    scopes
    )

/// get_my_partner_ids: resolves the current user's accessible partner IDs.
/// Replace the body with your auth provider integration.
/// With Supabase: join auth.uid() through your user->partner_member table.
let helperFunctions() = [|
    $"CREATE OR REPLACE FUNCTION {SCHEMA_NAME}.get_my_partner_ids()"
    "RETURNS TABLE (partner_id uuid)"
    "LANGUAGE sql"
    "SECURITY DEFINER"
    "SET search_path = ''"
    "AS $$"
    "    -- Replace with your auth integration."
    "    -- Example with Supabase: SELECT partner_id FROM app.partner_members"
    "    --   WHERE user_id = (SELECT id FROM auth.users WHERE id = auth.uid());"
    "    SELECT NULL::uuid WHERE FALSE;"
    "$$;"
|]

let rlsPolicy tableName scope =
    match scope with
    | RlsScope.Authenticated -> [|
        $"""CREATE POLICY "authenticated_access" ON {SCHEMA_NAME}.{tableName}"""
        "FOR SELECT"
        "TO authenticated"
        "USING (true);"
      |]
    | RlsScope.TenantOwn -> [|
        $"""CREATE POLICY "tenant_scoped" ON {SCHEMA_NAME}.{tableName}"""
        "FOR ALL"
        "TO authenticated"
        "USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)"
        "WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);"
      |]
    | RlsScope.CustomerOwn -> [|
        $"""CREATE POLICY "customer_scoped" ON {SCHEMA_NAME}.{tableName}"""
        "FOR ALL"
        "TO authenticated"
        "USING (customer_id IN ("
        "    SELECT customer_id"
        $"    FROM {SCHEMA_NAME}.customers"
        "    WHERE tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid"
        "      AND email = auth.jwt() ->> 'email'))"
        "WITH CHECK (customer_id IN ("
        "    SELECT customer_id"
        $"    FROM {SCHEMA_NAME}.customers"
        "    WHERE tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid"
        "      AND email = auth.jwt() ->> 'email'));"
      |]
    | RlsScope.PartnerOwn -> [|
        $"""CREATE POLICY "partner_scoped" ON {SCHEMA_NAME}.{tableName}"""
        "FOR ALL"
        "TO authenticated"
        $"USING (partner_id IN (SELECT partner_id FROM {SCHEMA_NAME}.get_my_partner_ids()))"
        $"WITH CHECK (partner_id IN (SELECT partner_id FROM {SCHEMA_NAME}.get_my_partner_ids()));"
      |]

let generateRlsPolicies() =
    let tableDefinitions = getTableDefinitions()

    [|
        yield $"""-- generated RLS SQL ({DateTimeOffset.UtcNow:``yyyy-MM-dd HH:mm:ss``} UTC)"""
        yield "\n-- helper functions"
        yield! helperFunctions()
        yield "\n-- RLS policies"
        yield!
            rlsScopes
            |> Array.map(fun (tableName, scopes) ->
                scopes
                |> Array.map(rlsPolicy tableName >> String.concat "\n")
                |> String.concat "\n\n"
                |> fun s -> $"{s}\n"
            )
        yield "\n-- end of RLS SQL"
    |]
    |> String.concat "\n"

#endif
