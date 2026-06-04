#r "bin/Debug/net10.0/FSharpPostgreSQL.dll"

open Database.PostgreSQL.Generator

let tableDefinitions = getTableDefinitions()
printfn "Found %d tables" tableDefinitions.Length

tableDefinitions
|> Array.iter (fun td ->
    try
        let sql = createTable td
        printfn "✓ %s: %d lines of SQL" td.TableName sql.Length
    with ex ->
        printfn "✗ %s: %s" td.TableName ex.Message
)
