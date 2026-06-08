# fsharp-postgresql

Reference implementation for the article [Your Database Schema Is Your Codebase: F# as the Single Source of Truth](https://si-fi.dev/articles/fsharp-schema-as-code).

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

### Development Workflow

**1. Edit your schema**

Modify `src/Shared/Schema.fs` to define your tables, enums, and relationships.

**2. Generate all SQL artefacts**

```bash
npm run generate:all
```

This runs all generation scripts and writes to `src/Database/Generated/`:
- `Schema.sql` — table definitions, constraints, enums
- `RlsPolicies.sql` — row-level security policies
- `Views.sql` — materialized views from quotations
- `Storage.sql` — Supabase storage bucket definitions
- `Functions.sql` — stored procedures (empty by default; add to `src/Database/FunctionGenerate.fsx`)

Individual scripts are also available:
```bash
npm run generate:schema
npm run generate:views
npm run generate:rls
npm run generate:functions
npm run generate:storage
```

**3. Review generated SQL**

Check `src/Database/Generated/` before deploying.

### Local Testing with Supabase

**Start Supabase locally:**

```bash
npm run supabase:start
```

This creates a local PostgreSQL instance on port 54322.

**Apply your generated schema to the local database:**

```bash
npm run supabase:push:all
```

This applies all generated SQL files in order:
- Schema, Views, RLS Policies, Functions, Storage

Individual commands:
```bash
npm run supabase:push:schema
npm run supabase:push:views
npm run supabase:push:rls
npm run supabase:push:functions
npm run supabase:push:storage
```

**Stop Supabase:**

```bash
npm run supabase:stop
```

### Production Deployment

The generated SQL files are committed to source control. For production deployments, use Supabase CLI:

```bash
supabase db diff --schema app > migrations/YYYYMMDDHHMMSS_description.sql
supabase db push
```

This creates an incremental migration file (applying only changes, not the full schema).

## Architecture

### Single Source of Truth

The schema is defined as F# record types with custom attributes in `src/Shared/Schema.fs`. This ensures:

- **Type safety**: Phantom-typed IDs (`GuidId<'T>`, `StringId<'T>`, `Int64Id<'T>`) prevent foreign key type confusion at compile time
- **Constraint clarity**: Attributes (`[<PK>]`, `[<FK>]`, `[<Unique>]`, `[<Default>]`, `[<WithoutOverlap>]`) express schema intent directly in code
- **Single definition**: No duplication between F# types and SQL schemas

### Two-Stage Workflow

**Development (Local)**: F# types → Complete SQL files

1. Edit `src/Shared/Schema.fs`
2. Run `npm run generate:all`
3. Review `src/Database/Generated/*.sql`
4. Test locally with `npm run supabase:start && npm run supabase:push:all`

**Production (Deployed)**: SQL files → Incremental migrations

1. Generate SQL files from updated `Schema.fs` (same as development)
2. Use Supabase CLI to compute schema diff: `supabase db diff`
3. Create versioned migration file in `supabase/migrations/`
4. Deploy with `supabase db push` (applies only changes)

This approach provides type-safe schema definition (F# → SQL) while maintaining safe, incremental production deployments (SQL diffs → versioned migrations).

### Row-Level Security

RLS policies are automatically generated from foreign key relationships in `Schema.fs` using `RlsGenerator.fs`. Multi-tenant isolation is enforced through `tenant_id` chains in the table structure.

To customize RLS behavior for your auth system, update the helper functions in `src/Database/RlsGenerator.fs`.

## Documentation

See [docs/](docs/README.md) for comprehensive guides:

- **[SCHEMA_WORKFLOW.md](docs/SCHEMA_WORKFLOW.md)** — Define and modify schema using F# types
- **[DATABASE_MIGRATION_STRATEGY.md](docs/DATABASE_MIGRATION_STRATEGY.md)** — Migration workflow and best practices  
- **[SUPABASE_SETUP.md](docs/SUPABASE_SETUP.md)** — Local and production Supabase setup

## Adapting to your schema

1. Edit `src/Shared/Schema.fs` to define your tables and enums
2. Run `npm run generate:all` to regenerate SQL files
3. Run `npm run supabase:push:all` to test locally
4. For RLS policies that reference custom auth functions, update `src/Database/RlsGenerator.fs`

See [docs/SCHEMA_WORKFLOW.md](docs/SCHEMA_WORKFLOW.md) for detailed walkthrough.

## Requirements

- .NET 10 SDK
- Node.js ≥ 18
- PostgreSQL client (`psql`)
