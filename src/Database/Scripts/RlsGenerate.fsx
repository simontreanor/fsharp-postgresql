// Build the project first: dotnet build fsharp-postgresql.fsproj
// Then run this script: dotnet fsi --shadowcopyreferences RlsGenerate.fsx
// The --shadowcopyreferences flag prevents DLL locking

#r "../../../bin/Debug/net10.0/FSharpPostgreSQL.dll"

let outputPath = "src/Database/Generated/RlsPolicies.sql"

Database.RlsGenerator.generateRlsPolicies()
|> fun sql -> System.IO.File.WriteAllText(outputPath, sql)

printfn "RLS SQL written to %s" outputPath
