-- generated PostgreSQL schema (2026-06-04 22:34:29 UTC)

-- schema
DROP SCHEMA IF EXISTS app CASCADE;
CREATE SCHEMA app;
GRANT USAGE ON SCHEMA app TO authenticated, service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA app GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO authenticated;
ALTER DEFAULT PRIVILEGES IN SCHEMA app GRANT ALL ON TABLES TO service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA app GRANT USAGE, SELECT ON SEQUENCES TO authenticated, service_role;

-- extensions
CREATE EXTENSION IF NOT EXISTS btree_gist SCHEMA extensions;

-- enum types
CREATE TYPE app.booking_status AS ENUM ('draft', 'pending', 'confirmed', 'cancelled', 'completed');

CREATE TYPE app.resource_type AS ENUM ('conference_room', 'equipment', 'processing_center');


-- tables
CREATE TABLE app.tenants (
    tenant_id uuid NOT NULL DEFAULT gen_random_uuid(),
    name text NOT NULL
);

CREATE TABLE app.partners (
    partner_id text NOT NULL,
    tenant_id uuid NOT NULL,
    name text NOT NULL,
    status app.booking_status NOT NULL DEFAULT 'draft'
);

CREATE TABLE app.partner_availabilities (
    window_id uuid NOT NULL DEFAULT gen_random_uuid(),
    partner_id text NOT NULL,
    slot_start timestamptz NOT NULL,
    slot_end timestamptz NOT NULL
);

CREATE TABLE app.customers (
    tenant_id uuid NOT NULL,
    customer_id uuid NOT NULL DEFAULT gen_random_uuid(),
    name text NOT NULL,
    email text NOT NULL
);

CREATE TABLE app.services (
    service_id text NOT NULL,
    tenant_id uuid NOT NULL,
    name text NOT NULL,
    duration_minutes int4 NOT NULL DEFAULT 60
);

CREATE TABLE app.resources (
    resource_id text NOT NULL,
    tenant_id uuid NOT NULL,
    partner_id text NOT NULL,
    name text NOT NULL,
    type app.resource_type NOT NULL
);

CREATE TABLE app.service_resources (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    service_id text NOT NULL,
    resource_id text NOT NULL,
    tenant_id uuid NOT NULL
);

CREATE TABLE app.bookings (
    booking_id uuid NOT NULL DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    partner_id text NOT NULL,
    customer_id uuid NOT NULL,
    service_id text NOT NULL,
    resource_id text NOT NULL,
    status app.booking_status NOT NULL DEFAULT 'draft',
    notes text
);

CREATE TABLE app.invoices (
    tenant_id uuid NOT NULL,
    invoice_id bigserial NOT NULL,
    booking_id uuid NOT NULL,
    amount numeric NOT NULL,
    issued_at timestamptz NOT NULL DEFAULT now()
);

-- primary keys
ALTER TABLE app.tenants ADD CONSTRAINT tenants_pkey PRIMARY KEY (tenant_id);
ALTER TABLE app.partners ADD CONSTRAINT partners_pkey PRIMARY KEY (partner_id);
ALTER TABLE app.partner_availabilities ADD CONSTRAINT partner_availabilities_pkey PRIMARY KEY (window_id);
ALTER TABLE app.customers ADD CONSTRAINT customers_pkey PRIMARY KEY (tenant_id, customer_id);
ALTER TABLE app.services ADD CONSTRAINT services_pkey PRIMARY KEY (service_id);
ALTER TABLE app.resources ADD CONSTRAINT resources_pkey PRIMARY KEY (resource_id);
ALTER TABLE app.service_resources ADD CONSTRAINT service_resources_pkey PRIMARY KEY (id);
ALTER TABLE app.bookings ADD CONSTRAINT bookings_pkey PRIMARY KEY (booking_id);
ALTER TABLE app.invoices ADD CONSTRAINT invoices_pkey PRIMARY KEY (tenant_id, invoice_id);
-- RLS
ALTER TABLE app.tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.tenants FORCE ROW LEVEL SECURITY;

ALTER TABLE app.partners ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.partners FORCE ROW LEVEL SECURITY;

ALTER TABLE app.partner_availabilities ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.partner_availabilities FORCE ROW LEVEL SECURITY;

ALTER TABLE app.customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.customers FORCE ROW LEVEL SECURITY;

ALTER TABLE app.services ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.services FORCE ROW LEVEL SECURITY;

ALTER TABLE app.resources ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.resources FORCE ROW LEVEL SECURITY;

ALTER TABLE app.service_resources ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.service_resources FORCE ROW LEVEL SECURITY;

ALTER TABLE app.bookings ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.bookings FORCE ROW LEVEL SECURITY;

ALTER TABLE app.invoices ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.invoices FORCE ROW LEVEL SECURITY;

-- unique constraints
CREATE UNIQUE INDEX partner_name_unique_within_tenant ON app.partners (tenant_id, name) NULLS NOT DISTINCT;
ALTER TABLE app.partners ADD CONSTRAINT partner_name_unique_within_tenant UNIQUE USING INDEX partner_name_unique_within_tenant;

CREATE UNIQUE INDEX service_resource_unique ON app.service_resources (service_id, resource_id, tenant_id) NULLS NOT DISTINCT;
ALTER TABLE app.service_resources ADD CONSTRAINT service_resource_unique UNIQUE USING INDEX service_resource_unique;

-- foreign keys
ALTER TABLE app.partners ADD CONSTRAINT partners_tenants_fkey FOREIGN KEY (tenant_id) REFERENCES app.tenants(tenant_id);
ALTER TABLE app.partner_availabilities ADD CONSTRAINT partner_availabilities_partners_fkey FOREIGN KEY (partner_id) REFERENCES app.partners(partner_id);
ALTER TABLE app.customers ADD CONSTRAINT customers_tenants_fkey FOREIGN KEY (tenant_id) REFERENCES app.tenants(tenant_id);
ALTER TABLE app.services ADD CONSTRAINT services_tenants_fkey FOREIGN KEY (tenant_id) REFERENCES app.tenants(tenant_id);
ALTER TABLE app.resources ADD CONSTRAINT resources_tenants_fkey FOREIGN KEY (tenant_id) REFERENCES app.tenants(tenant_id);
ALTER TABLE app.resources ADD CONSTRAINT resources_partners_fkey FOREIGN KEY (partner_id) REFERENCES app.partners(partner_id);
ALTER TABLE app.service_resources ADD CONSTRAINT service_resources_services_fkey FOREIGN KEY (service_id) REFERENCES app.services(service_id);
ALTER TABLE app.service_resources ADD CONSTRAINT service_resources_resources_fkey FOREIGN KEY (resource_id) REFERENCES app.resources(resource_id);
ALTER TABLE app.service_resources ADD CONSTRAINT service_resources_tenants_fkey FOREIGN KEY (tenant_id) REFERENCES app.tenants(tenant_id);
ALTER TABLE app.bookings ADD CONSTRAINT bookings_tenants_fkey FOREIGN KEY (tenant_id) REFERENCES app.tenants(tenant_id);
ALTER TABLE app.bookings ADD CONSTRAINT bookings_customers_fkey FOREIGN KEY (tenant_id, customer_id) REFERENCES app.customers(tenant_id, customer_id);
ALTER TABLE app.bookings ADD CONSTRAINT bookings_partners_fkey FOREIGN KEY (partner_id) REFERENCES app.partners(partner_id);
ALTER TABLE app.bookings ADD CONSTRAINT bookings_services_fkey FOREIGN KEY (service_id) REFERENCES app.services(service_id);
ALTER TABLE app.bookings ADD CONSTRAINT bookings_resources_fkey FOREIGN KEY (resource_id) REFERENCES app.resources(resource_id);
ALTER TABLE app.invoices ADD CONSTRAINT invoices_tenants_fkey FOREIGN KEY (tenant_id) REFERENCES app.tenants(tenant_id);
ALTER TABLE app.invoices ADD CONSTRAINT invoices_bookings_fkey FOREIGN KEY (booking_id) REFERENCES app.bookings(booking_id);
-- without-overlap constraints
ALTER TABLE app.partner_availabilities ADD CONSTRAINT partner_availabilities_without_overlaps EXCLUDE USING gist (tstzrange(slot_start, slot_end) with &&);

-- end of schema