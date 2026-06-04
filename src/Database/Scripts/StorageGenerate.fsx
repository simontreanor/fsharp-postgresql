// Build the project first: dotnet build fsharp-postgresql.fsproj
// Then run this script: dotnet fsi --shadowcopyreferences StorageGenerate.fsx
// The --shadowcopyreferences flag prevents DLL locking

#r "../../../bin/Debug/net10.0/FSharpPostgreSQL.dll"

let outputPath = "src/Database/Generated/Storage.sql"

Database.StorageGenerator.generateAllStorage()
|> fun sql -> System.IO.File.WriteAllText(outputPath, sql)

printfn "Storage SQL written to %s" outputPath
