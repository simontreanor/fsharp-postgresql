# Database Migration Strategy

## Overview

This document outlines the process for managing database schema changes using the schema-as-code approach.

## Schema Definition

- Database schema is defined in F# types in [src/Shared/Schema.fs](../src/Shared/Schema.fs)
- Type-safe approach ensures compile-time validation
- Schema generators convert F# types to SQL

## Migration Process

### 1. Modify Schema

Edit the F# types in [src/Shared/Schema.fs](../src/Shared/Schema.fs):
- Add/modify table types
- Add/modify enum types
- Add/modify attributes (PK, FK, Unique, Default, WithoutOverlap)

Example:
```fsharp
type Service = {
    [<PK>]
    ServiceId: StringId<Service>
    [<FK(typeof<Tenant>)>]
    TenantId: GuidId<Tenant>
    Name: string
    [<Default("60")>]
    DurationMinutes: int
}
```

### 2. Generate SQL from F# Schema

Use the npm scripts to generate SQL from your F# types:

```bash
# Generate all SQL files
npm run generate:all

# Or generate individual components
npm run generate:schema
npm run generate:views
npm run generate:rls
npm run generate:functions
npm run generate:storage
```

This creates/updates files in `src/Database/Generated/`:
- `Schema.sql` — table definitions, enums, constraints
- `Views.sql` — view definitions
- `RlsPolicies.sql` — row-level security policies
- `Functions.sql` — stored procedures
- `Storage.sql` — storage bucket definitions

### 3. Test Locally with Supabase

Start a local Supabase instance and apply the generated schema:

```bash
# Start Supabase (creates local PostgreSQL on port 54322)
npm run supabase:start

# Apply all generated SQL files to local database
npm run supabase:push:all

# Or apply individual components
npm run supabase:push:schema
npm run supabase:push:views
npm run supabase:push:rls
```

### 4. Review Generated SQL Files

Check the generated SQL files in `src/Database/Generated/`:
- `Schema.sql` — table definitions, enums, constraints
- `RlsPolicies.sql` — row-level security policies
- `Views.sql` — database views
- `Functions.sql` — stored procedures
- `Storage.sql` — storage bucket definitions

### 5. Commit Schema Changes

Commit both the F# schema and all generated SQL files:

```bash
git add src/Shared/Schema.fs
git add src/Database/Generated/
git commit -m "schema: add new table"
```

The generated SQL files are committed to source control as the source of truth for the current schema state.

### 6. Create Production Migration (Supabase CLI)

For production deployments, use Supabase CLI to create incremental migration files:

```bash
# Link to your remote Supabase project (one-time setup)
npx supabase link --project-ref <your-project-ref>

# Generate migration diff comparing local schema with remote database
npx supabase db diff --schema app > supabase/migrations/YYYYMMDDHHMMSS_description.sql

# Review the migration file
cat supabase/migrations/YYYYMMDDHHMMSS_description.sql
```

The `db diff` command:
- Compares your local schema (generated SQL) with the remote database
- Creates an incremental migration file with only the changes
- Stores it in `supabase/migrations/` for version control

### 7. Apply Production Migration

```bash
# Deploy to production
npx supabase db push
```

This applies only the changes specified in the migration file, not the entire schema.

---

## Important Notes

### RLS Policies

Row Level Security policies are **automatically generated** from foreign key relationships in `Schema.fs`.

**How it works:**
1. RLS policies are inferred from FK relationships in the schema
2. The `RlsGenerator.fs` analyzes table dependencies
3. Run `npm run generate:rls` to regenerate policies after schema changes
4. Output: `src/Database/Generated/RlsPolicies.sql`

**Deployment notes:**
- After applying RLS changes to production, refresh authentication hooks in Supabase Dashboard
- Custom policies (like storage bucket policies) are generated separately in `Storage.sql`

### Data Migrations

- **Never hardcode generated IDs** (UUIDs, auto-increments)
- Use `INSERT ... RETURNING id` and reference in subsequent statements

```sql
-- ✅ Correct: Use RETURNING and reference in next statement
WITH new_tenant AS (
  INSERT INTO app.tenants (name) 
  VALUES ('Acme Corp')
  RETURNING tenant_id
)
INSERT INTO app.partners (tenant_id, name)
SELECT tenant_id, 'Partner 1' FROM new_tenant;

-- ❌ Incorrect: hardcoded ID
INSERT INTO app.tenants (tenant_id, name) 
VALUES ('123e4567-e89b-12d3-a456-426614174000', 'Acme Corp');
```

### Testing Migrations

1. Test locally first with `npm run supabase:push:*` commands
2. Verify all data is accessible with RLS enabled
3. Test rollback strategy if needed
4. Only push to production after thorough testing

---

## Workflow Summary

This project uses a **two-stage approach** for schema management:

**Stage 1: Development (F# → Complete Schema)**
- Define schema as F# types in `Schema.fs`
- Generate complete SQL files with `npm run generate:all`
- Provides type safety and IDE support
- All generated files committed to source control

**Stage 2: Production (SQL → Incremental Migrations)**
- Use Supabase CLI `db diff` to compute schema changes
- Create versioned migration files in `supabase/migrations/`
- Deploy with `supabase db push` (applies only changes)
- Safe, auditable, reversible deployments

This ensures:
- ✅ Type-safe schema definitions
- ✅ Single source of truth (F# types)
- ✅ Complete schema in source control
- ✅ Safe incremental production deployments
