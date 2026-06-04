#r "bin/Debug/net10.0/FSharpPostgreSQL.dll"

open System.Reflection

let tables = typeof<Shared.Schema.Tables.Tenant>.DeclaringType.GetNestedTypes(BindingFlags.Public ||| BindingFlags.NonPublic)

tables
|> Array.map _.Name
|> Array.sort
|> Array.iter (fun name -> printfn "Type: %s" name)

printfn "\nTotal types: %d" tables.Length
