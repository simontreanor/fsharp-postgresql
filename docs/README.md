# Documentation

## Overview

- **[SCHEMA_WORKFLOW.md](SCHEMA_WORKFLOW.md)** — How to define and modify database schema using F# types
- **[DATABASE_MIGRATION_STRATEGY.md](DATABASE_MIGRATION_STRATEGY.md)** — Two-stage migration approach (F# → SQL → incremental migrations)
- **[SUPABASE_SETUP.md](SUPABASE_SETUP.md)** — Setting up Supabase for local development and production

## Quick Start

1. Clone the repo
2. Read [SUPABASE_SETUP.md](SUPABASE_SETUP.md) → local development
3. Read [SCHEMA_WORKFLOW.md](SCHEMA_WORKFLOW.md) → editing schema
4. Read [DATABASE_MIGRATION_STRATEGY.md](DATABASE_MIGRATION_STRATEGY.md) → production deployment

## Key Concepts

**Schema-as-Code**: Database schema is defined as F# record types in `src/Shared/Schema.fs` with custom attributes. This single source of truth is compiled to SQL using reflection-based generators.

**Two-Stage Migration**: Development uses generated SQL files (for complete schema definition), while production uses Supabase CLI's `db diff` to create incremental migrations.

**Type Safety**: Phantom-typed IDs (`GuidId<'T>`, `StringId<'T>`, `Int64Id<'T>`) prevent compile-time foreign key type confusion.

**Automatic RLS**: Row-level security policies are generated from foreign key relationships, ensuring multi-tenant isolation is enforced consistently.

## Environment

- .NET 10 SDK
- Node.js ≥ 18 (for Supabase CLI)
- PostgreSQL client (`psql`)
