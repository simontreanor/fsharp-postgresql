-- generated RLS SQL (2026-06-04 22:22:19 UTC)

-- helper functions
CREATE OR REPLACE FUNCTION app.get_my_partner_ids()
RETURNS TABLE (partner_id uuid)
LANGUAGE sql
SECURITY DEFINER
SET search_path = ''
AS $$
    -- Replace with your auth integration.
    -- Example with Supabase: SELECT partner_id FROM app.partner_members
    --   WHERE user_id = (SELECT id FROM auth.users WHERE id = auth.uid());
    SELECT NULL::uuid WHERE FALSE;
$$;

-- RLS policies
CREATE POLICY "authenticated_access" ON app.tenants
FOR SELECT
TO authenticated
USING (true);

CREATE POLICY "tenant_scoped" ON app.tenants
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);

CREATE POLICY "tenant_scoped" ON app.partners
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);

CREATE POLICY "partner_scoped" ON app.partners
FOR ALL
TO authenticated
USING (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()))
WITH CHECK (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()));

CREATE POLICY "partner_scoped" ON app.partner_availabilities
FOR ALL
TO authenticated
USING (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()))
WITH CHECK (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()));

CREATE POLICY "tenant_scoped" ON app.customers
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);

CREATE POLICY "customer_scoped" ON app.customers
FOR ALL
TO authenticated
USING (customer_id IN (
    SELECT customer_id
    FROM app.customers
    WHERE tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
      AND email = auth.jwt() ->> 'email'))
WITH CHECK (customer_id IN (
    SELECT customer_id
    FROM app.customers
    WHERE tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
      AND email = auth.jwt() ->> 'email'));

CREATE POLICY "authenticated_access" ON app.services
FOR SELECT
TO authenticated
USING (true);

CREATE POLICY "tenant_scoped" ON app.services
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);

CREATE POLICY "tenant_scoped" ON app.resources
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);

CREATE POLICY "partner_scoped" ON app.resources
FOR ALL
TO authenticated
USING (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()))
WITH CHECK (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()));

CREATE POLICY "authenticated_access" ON app.service_resources
FOR SELECT
TO authenticated
USING (true);

CREATE POLICY "tenant_scoped" ON app.service_resources
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);

CREATE POLICY "tenant_scoped" ON app.bookings
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);

CREATE POLICY "customer_scoped" ON app.bookings
FOR ALL
TO authenticated
USING (customer_id IN (
    SELECT customer_id
    FROM app.customers
    WHERE tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
      AND email = auth.jwt() ->> 'email'))
WITH CHECK (customer_id IN (
    SELECT customer_id
    FROM app.customers
    WHERE tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
      AND email = auth.jwt() ->> 'email'));

CREATE POLICY "partner_scoped" ON app.bookings
FOR ALL
TO authenticated
USING (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()))
WITH CHECK (partner_id IN (SELECT partner_id FROM app.get_my_partner_ids()));

CREATE POLICY "authenticated_access" ON app.invoices
FOR SELECT
TO authenticated
USING (true);

CREATE POLICY "tenant_scoped" ON app.invoices
FOR ALL
TO authenticated
USING (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid)
WITH CHECK (tenant_id = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid);


-- end of RLS SQL