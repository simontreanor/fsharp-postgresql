#r "bin/Debug/net10.0/FSharpPostgreSQL.dll"

open Database.PostgreSQL.Generator

let enums = getEnums()
printfn "Found %d enums" enums.Length
enums |> Array.iter (fun e -> printfn "  - %s" e.Name)

let sql = createEnumTypes()
printfn "\nEnum SQL:\n%s" (String.concat "\n" sql)
