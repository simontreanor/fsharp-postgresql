# Supabase Project Setup

**Last updated:** 2026-06-06

This document covers creating and provisioning a new Supabase project on [supabase.com](https://supabase.com) for local development and production deployment.

---

## Prerequisites

- **Node ≥ 18** with `npx` — the Supabase CLI is in `devDependencies`, no global install needed
- **`psql`** on `PATH` — install via Chocolatey if not present: `choco install postgresql`
- **PowerShell** 7+ or bash

---

## Local Development Setup

### Step 1 — Create `.env` File

Create `.env` in the project root (already gitignored):

```env
# Local development
VITE_APP_URL=http://localhost:5173
SCHEMA_NAME=app

# Supabase local development (from supabase start)
VITE_SUPABASE_API_PROJECT_URL=http://localhost:54321
VITE_SUPABASE_AUTH_KEY_PUBLIC=sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH
VITE_SUPABASE_FUNCTIONS_URL=http://localhost:54321/functions/v1
```

The public key and project URL shown above are defaults for local Supabase instances and are safe to use for development.

### Step 2 — Start Supabase Locally

```bash
npm run supabase:start
```

This creates:
- PostgreSQL database on port `54322` (connection: `postgresql://postgres:postgres@localhost:54322/postgres`)
- PostgREST API on port `54321`
- Studio (web UI) on port `54323`
- Inbucket (email testing) on port `54324`

### Step 3 — Apply the Bootstrap Migration

```bash
npm run supabase:push:schema
```

This creates the `app` schema and sets permissions. Safe to re-run (uses `IF NOT EXISTS`).

### Step 4 — Generate and Apply Your Schema

```bash
# Generate SQL from F# schema
npm run generate:all

# Apply all generated SQL to local database
npm run supabase:push:all
```

This applies:
1. `Schema.sql` — tables and enums
2. `Views.sql` — database views
3. `RlsPolicies.sql` — row-level security
4. `Functions.sql` — stored procedures
5. `Storage.sql` — storage buckets

### Step 5 — Verify

Check the [Supabase Studio](http://localhost:54323):
- Tables are visible under `app` schema
- Storage buckets appear under Storage
- RLS policies are listed under Authentication → Policies

---

## Production Setup

### Step 1 — Create Supabase Project

Either via the [Supabase Dashboard](https://supabase.com/dashboard) or via CLI:

```bash
npx supabase login
npx supabase orgs list            # note your org-id
npx supabase projects create my-app \
    --org-id <org-id> \
    --region eu-west-2 \
    --db-password <strong-password>
```

Note the **project ref** (shown in dashboard URL and all API URLs).

### Step 2 — Create `.env.production` File

Create `.env.production` in the project root (gitignored — never commit):

```env
# Production Supabase project
VITE_SUPABASE_API_PROJECT_URL=https://<ref>.supabase.co
VITE_SUPABASE_AUTH_KEY_PUBLIC=<anon-public-key>
VITE_SUPABASE_FUNCTIONS_URL=https://<ref>.supabase.co/functions/v1

# App
VITE_APP_URL=https://yourdomain.com
SCHEMA_NAME=app
```

Get the API URL and anon key from **Project Settings → API** in the Supabase dashboard.

### Step 3 — Link to Remote Project

Set required environment variables and link:

```bash
export VITE_SUPABASE_API_PROJECT_URL="https://<ref>.supabase.co"
export VITE_APP_URL="https://yourdomain.com"
export SCHEMA_NAME="app"

npx supabase link --project-ref <ref>
```

### Step 4 — Apply Bootstrap Migration

```bash
npx supabase db push
```

This runs `supabase/migrations/00000000000000_bootstrap_schema.sql`, creating the `app` schema.

### Step 5 — Generate and Apply Schema

```bash
# Generate SQL from F# schema
npm run generate:all

# Get remote connection string from Supabase Dashboard
# Project Settings → Database → Connection string (Transaction pooler)
psql "postgresql://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:6543/postgres" \
  -f src/Database/Generated/Schema.sql

# Apply remaining generated files
psql "postgresql://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:6543/postgres" \
  -f src/Database/Generated/Views.sql

psql "postgresql://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:6543/postgres" \
  -f src/Database/Generated/RlsPolicies.sql

psql "postgresql://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:6543/postgres" \
  -f src/Database/Generated/Functions.sql

psql "postgresql://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:6543/postgres" \
  -f src/Database/Generated/Storage.sql
```

Or use the Supabase CLI with incremental migrations:

```bash
npx supabase db diff --schema app > supabase/migrations/$(date +%s)_schema.sql
npx supabase db push
```

### Step 6 — Verify Production Schema

- Check **Project Settings → API** to confirm `app` schema is in extra search path
- Check **Storage** to verify buckets are created
- Check **Authentication → Policies** to verify RLS is enabled

---

## Environment Variables Reference

| Variable | Purpose | Example |
|---|---|---|
| `SCHEMA_NAME` | PostgreSQL schema name | `app` |
| `VITE_SUPABASE_API_PROJECT_URL` | Supabase project URL | `http://localhost:54321` or `https://<ref>.supabase.co` |
| `VITE_SUPABASE_AUTH_KEY_PUBLIC` | Public (anon) API key | From Supabase Dashboard |
| `VITE_SUPABASE_FUNCTIONS_URL` | Edge Functions endpoint | `http://localhost:54321/functions/v1` |
| `VITE_APP_URL` | Application URL | `http://localhost:5173` or `https://yourdomain.com` |

---

## Useful Commands

```bash
# Start local Supabase
npm run supabase:start

# Stop local Supabase
npm run supabase:stop

# Generate all SQL from F# schema
npm run generate:all

# Apply all generated SQL to local database
npm run supabase:push:all

# View Supabase Studio (local)
# Open http://localhost:54323

# Connect to local database with psql
psql postgresql://postgres:postgres@localhost:54322/postgres

# Create incremental migration for production
npx supabase db diff --schema app > supabase/migrations/$(date +%s)_description.sql
npx supabase db push
```

---

## Troubleshooting

**Q: `supabase start` fails with "port already in use"**
- Kill existing Supabase: `npm run supabase:stop`
- Check if Docker daemon is running

**Q: `psql` command not found**
- Install PostgreSQL client: `choco install postgresql`
- Verify it's on PATH: `psql --version`

**Q: Cannot connect to remote database**
- Verify project ref in `.env.production`
- Use Transaction pooler connection string (port 6543), not Session pooler
- Check firewall rules allow outbound HTTPS to Supabase region

**Q: RLS policies not applying**
- Ensure `RlsPolicies.sql` was applied after `Schema.sql`
- Verify auth user is linked to `auth.uid()` in your app
- Check policies in Supabase Studio under Authentication → Policies

---

## Next Steps

See [DATABASE_MIGRATION_STRATEGY.md](DATABASE_MIGRATION_STRATEGY.md) for the schema modification workflow.
