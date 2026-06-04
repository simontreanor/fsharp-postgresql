// Build the project first: dotnet build fsharp-postgresql.fsproj
// Then run this script: dotnet fsi --shadowcopyreferences SchemaGenerate.fsx
// The --shadowcopyreferences flag prevents DLL locking

#r "../../../bin/Debug/net10.0/FSharpPostgreSQL.dll"

let outputPath = "src/Database/Generated/Schema.sql"

Database.PostgreSQL.Generator.generateSchemaSql()
|> fun sql -> System.IO.File.WriteAllText(outputPath, sql)

printfn "Schema SQL written to %s" outputPath
