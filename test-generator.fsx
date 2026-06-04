#r "bin/Debug/net10.0/FSharpPostgreSQL.dll"

open Database.PostgreSQL.Generator

let tables = getTableDefinitions()
printfn "Found %d tables" tables.Length
tables |> Array.iter (fun td -> printfn "  - %s (%d columns)" td.TableName td.Columns.Length)
