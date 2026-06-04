// Build the project first: dotnet build fsharp-postgresql.fsproj
// Then run this script: dotnet fsi --shadowcopyreferences ViewGenerate.fsx
// The --shadowcopyreferences flag prevents DLL locking

#r "../../../bin/Debug/net10.0/FSharpPostgreSQL.dll"

let outputPath = "src/Database/Generated/Views.sql"

Database.ViewGenerator.generateAllViews()
|> fun sql -> System.IO.File.WriteAllText(outputPath, sql)

printfn "View SQL written to %s" outputPath
