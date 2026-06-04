# fsharp-postgresql

Reference implementation for the article [Your Database Schema Is Your Codebase: F# as the Single Source of Truth](https://si-fi.dev/articles/FSharpSchemaAsCode).

Demonstrates a schema-as-code approach where F# record types with custom attributes are the single source of truth for PostgreSQL table shape, keys, constraints, RLS policies, and views.

## Structure

```
src/
  Shared/
    Grammar.fs          string utilities (toSnakeCase, pluralise)
    Types.fs            phantom-typed ID wrappers (GuidId<'T>, StringId<'T>, Int64Id<'T>)
    Attributes.fs       schema attributes ([<PK>], [<FK>], [<Unique>], [<Default>], [<WithoutOverlap>])
    Config.fs           schema name constant
    Schema.fs           demo schema — edit this to define your tables
  Database/
    SeedData.fs         demo data arrays (empty — used as type witnesses by view generator)
    LocalViews.fs       F# quotation view definitions
    PostgreSQL.fs       schema SQL generator
    RlsGenerator.fs     RLS policy generator
    ViewGenerator.fs    view SQL generator (translates F# quotations to SQL JOINs)
    StorageGenerator.fs storage bucket and policy generator
    Generated/          generated SQL artefacts (committed to source control)
    Scripts/            dotnet fsi generation scripts
```

## Usage

**1. Build**

```
dotnet build fsharp-postgresql.fsproj
```

**2. Generate SQL artefacts**

Run each script from the repository root:

```
dotnet fsi --shadowcopyreferences src/Database/Scripts/SchemaGenerate.fsx
dotnet fsi --shadowcopyreferences src/Database/Scripts/RlsGenerate.fsx
dotnet fsi --shadowcopyreferences src/Database/Scripts/ViewGenerate.fsx
dotnet fsi --shadowcopyreferences src/Database/Scripts/StorageGenerate.fsx
```

Each script writes to `src/Database/Generated/`.

**3. Inspect output**

Review the generated SQL in `src/Database/Generated/` before applying to your database.

## Adapting to your schema

Edit `src/Shared/Schema.fs` to define your own tables and enums.

Edit `src/Database/LocalViews.fs` to define typed view quotations.

Update `src/Shared/Config.fs` to set your schema name.

For RLS policies that reference your own auth helper functions, update the `helperFunctions` body in `src/Database/RlsGenerator.fs`.

## Requirements

- .NET 10 SDK
