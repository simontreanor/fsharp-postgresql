# Skill: Change Schema

## When to use this skill

Read this before editing the database schema — covers the generation workflow, RLS policy generation, local Supabase setup, and migration strategy.

---

## Schema Change Workflow

### Quick Start

1. Edit `src/Shared/Schema.fs` — the sole source of truth
2. Run `npm run generate:all` — regenerates all SQL files
3. Run `npm run supabase:push:all` — applies SQL to local Supabase instance
4. Commit `Schema.fs` and `src/Database/Generated/*`

> **Note:** Always run `npm run generate:all` before `npm run supabase:push:all` if you've modified `Schema.fs`. The `generate:*` scripts include `dotnet build`; the `supabase:push:*` scripts only apply SQL.

---

## Schema Definition — F# Types

Schema is defined in `src/Shared/Schema.fs` using F# types and custom attributes.

### Tables

```fsharp
type Partner = {
    [<PK>]
    PartnerId: StringId<Partner>
    
    [<FK(typeof<Tenant>)>]
    TenantId: GuidId<Tenant>
    
    Name: string
    Email: string
}
```

### Enums

```fsharp
[<StringEnum(CaseRules.SnakeCase)>]
type BookingStatus =
    | Draft
    | Pending
    | Confirmed
    | Cancelled
    | Completed
```

### Key Attributes

| Attribute | Effect | Example |
|---|---|---|
| `[<PK>]` | Primary key | `[<PK>] BookingId: GuidId<Booking>` |
| `[<FK(typeof<T>)>]` | Foreign key reference | `[<FK(typeof<Tenant>)>] TenantId: GuidId<Tenant>` |
| `[<Unique(group)>]` | Unique constraint (same group string = same constraint) | `[<Unique("partner_unique")>]` |
| `[<Default(sql)>]` | SQL DEFAULT clause (include quotes for literals) | `[<Default("60")>]`, `[<Default("'draft'")>]` |
| `[<WithoutOverlap>]` | Exclusion constraint (timestamptz ranges, UUID equality) | `[<WithoutOverlap>] SlotStart: DateTimeOffset` |

### Phantom-Typed IDs

The schema uses three phantom-typed ID wrappers to prevent compile-time foreign key confusion:

```fsharp
[<Erase>]
type GuidId<'T> = GuidId of Guid        // UUID primary keys (tenants, bookings)

[<Erase>]
type StringId<'T> = StringId of string  // Natural string keys (services, resources)

[<Erase>]
type Int64Id<'T> = Int64Id of int64    // Sequential bigint keys (invoices)
```

Example:
```fsharp
type Service = {
    [<PK>]
    ServiceId: StringId<Service>        // Can't accidentally use with Resource FK
    [<FK(typeof<Tenant>)>]
    TenantId: GuidId<Tenant>            // Can't accidentally use with Customer FK
}

type Resource = {
    [<PK>]
    ResourceId: StringId<Resource>      // Different phantom type — compile error if mixed
    [<FK(typeof<Tenant>)>]
    TenantId: GuidId<Tenant>
}
```

---

## Generation Workflow

### 1. Modify Schema.fs

Edit table/enum definitions as needed. Example:

```fsharp
type Service = {
    [<PK>]
    ServiceId: StringId<Service>
    [<FK(typeof<Tenant>)>]
    TenantId: GuidId<Tenant>
    Name: string
    [<Default("60")>]
    DurationMinutes: int
    Location: string option              // ← New field
}
```

### 2. Generate SQL

```bash
npm run generate:all
```

This runs:
- `dotnet build` (compiles F# schema)
- Generates `Schema.sql` (table DDL, enums, constraints)
- Generates `Views.sql` (quotation-based views)
- Generates `RlsPolicies.sql` (RLS policies from FK relationships)
- Generates `Functions.sql` (stored procedures)
- Generates `Storage.sql` (storage bucket definitions)

All files written to `src/Database/Generated/`.

### 3. Test Locally

```bash
npm run supabase:push:all
```

This applies all generated SQL to local database (port 54322).

**Check Supabase Studio** at [http://localhost:54323](http://localhost:54323) to verify tables, enums, RLS policies, and storage buckets.

### 4. Review Generated SQL

Always review generated SQL before committing:

```bash
# Review schema
cat src/Database/Generated/Schema.sql

# Review RLS policies
cat src/Database/Generated/RlsPolicies.sql

# Verify no Odyso references or proprietary info remains
grep -i "odyso\|proprietary\|domain" src/Database/Generated/*.sql
```

### 5. Commit Changes

```bash
git add src/Shared/Schema.fs
git add src/Database/Generated/
git commit -m "schema: add Location field to Service table"
```

---

## RLS Generation

Row Level Security policies are **automatically generated** from FK relationships in `Schema.fs`.

### How It Works

1. RLS policies are inferred from FK relationships
2. The `RlsGenerator.fs` analyzes table topology to derive scope types
3. Scope types determined by column presence:
   - `tenant_id` → TenantOwn (user's tenant only)
   - `partner_id` → PartnerOwn (user's partner(s) only)
   - `customer_id` → CustomerOwn (user's customer only)
   - Composite: multi-column access control
4. RLS generator creates policies for each scope

### After Schema Changes

```bash
npm run generate:rls
```

This regenerates `src/Database/Generated/RlsPolicies.sql` based on current table structure.

### Customization

For RLS policies that reference your specific auth system (JWT claims, custom helper functions, etc.), update the `helperFunctions` section in `src/Database/RlsGenerator.fs`:

```fsharp
// In RlsGenerator.fs
let helperFunctions () = """
-- Your custom helper functions that RLS policies reference
-- Example: get_current_user_id(), get_my_partner_ids(), etc.
"""
```

---

## Generated Files

All files under `src/Database/Generated/` are generated — do not edit them directly.

| File | Contents | Generated By |
|---|---|---|
| `Schema.sql` | Table definitions, enums, constraints | PostgreSQL.fs |
| `RlsPolicies.sql` | Row Level Security policies | RlsGenerator.fs |
| `Views.sql` | Database views (from quotations) | ViewGenerator.fs |
| `Functions.sql` | Stored procedures | FunctionGenerate.fsx (empty by default) |
| `Storage.sql` | Storage buckets and RLS policies | StorageGenerator.fs |

---

## Data Migrations — Key Conventions

### Never Hardcode Generated IDs

```sql
-- ❌ WRONG: Hardcoded UUID
INSERT INTO app.tenants (tenant_id, name) 
VALUES ('550e8400-e29b-41d4-a716-446655440000', 'Acme Corp');

-- ✅ CORRECT: Use RETURNING and reference
WITH new_tenant AS (
  INSERT INTO app.tenants (name) 
  VALUES ('Acme Corp')
  RETURNING tenant_id
)
INSERT INTO app.partners (tenant_id, name)
SELECT tenant_id, 'Partner 1' FROM new_tenant;
```

### Multi-Step Data Migrations

```sql
-- ✅ Use CTE chains for multi-step operations
WITH new_tenant AS (
  INSERT INTO app.tenants (name) 
  VALUES ('Example Org')
  RETURNING tenant_id
),
new_customer AS (
  INSERT INTO app.customers (tenant_id, customer_id, name, email)
  SELECT tenant_id, gen_random_uuid(), 'John Doe', 'john@example.com'
  FROM new_tenant
  RETURNING tenant_id, customer_id
)
INSERT INTO app.bookings (booking_id, tenant_id, customer_id, status)
SELECT gen_random_uuid(), tenant_id, customer_id, 'draft'
FROM new_customer;
```

---

## Workflow Summary

```
Edit Schema.fs
         ↓
npm run generate:all
         ↓
Review Generated/*.sql
         ↓
npm run supabase:push:all (local testing)
         ↓
Test & verify
         ↓
git add & commit
         ↓
Deploy to production (supabase db push)
```

This ensures:
- ✅ Type-safe schema definitions (F# types)
- ✅ Single source of truth (Schema.fs)
- ✅ Automatic RLS generation from FK relationships
- ✅ Complete schema in source control (Generated/*.sql)
- ✅ Safe local testing before production deployment
