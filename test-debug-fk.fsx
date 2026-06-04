#r "bin/Debug/net10.0/FSharpPostgreSQL.dll"

open System.Reflection
open System.Collections.Generic
open Database.PostgreSQL

let tableDefinitions = Generator.getTableDefinitions()

let serviceResourceTd = tableDefinitions |> Array.find (fun td -> td.TableName = "service_resources")
printfn "ServiceResource columns:"
serviceResourceTd.Columns
|> Array.iter (fun col ->
    printfn "  %s (%s):" col.Name col.Type
    col.ForeignKeyTables
    |> Array.iter (fun fk ->
        printfn "    FK to %s (prefix: '%s')" fk.TableName fk.Prefix
    )
)

let partnerTd = tableDefinitions |> Array.find (fun td -> td.TableName = "partners")
printfn "\nPartner PKs:"
partnerTd.Columns
|> Array.filter _.IsPrimaryKey
|> Array.iter (fun col -> printfn "  %s" col.Name)
