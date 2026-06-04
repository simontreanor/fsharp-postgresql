-- generated views (2026-06-04 22:22:22 UTC)

-- booking_details: 4-table join (bookings → partners, customers, services, resources)
CREATE VIEW app.booking_details WITH (security_invoker = ON) AS
    SELECT
        b.tenant_id AS tenant_id,
            b.booking_id AS booking_id,
            p.name AS partner_name,
            b.customer_id AS customer_id,
            b.service_id AS service_id,
            b.resource_id AS resource_id,
            b.status AS status,
            b.notes AS notes
    FROM app.bookings b
    JOIN app.partners p ON p.partner_id = b.partner_id
;

-- service_availability: service-resource join via service_resources
CREATE VIEW app.service_availability WITH (security_invoker = ON) AS
    SELECT
        s.service_id AS service_id,
            s.name AS service_name,
            r.resource_id AS resource_id,
            r.name AS resource_name
    FROM app.service_resources sr
    JOIN app.services s ON s.service_id = sr.service_id
    JOIN app.resources r ON r.resource_id = sr.resource_id
;

-- invoice_details: 2-table join (invoices → bookings)
CREATE VIEW app.invoice_details WITH (security_invoker = ON) AS
    SELECT
        inv.tenant_id AS tenant_id,
            inv.invoice_id AS invoice_id,
            b.booking_id AS booking_id,
            inv.amount AS amount,
            inv.issued_at AS issued_at
    FROM app.invoices inv
    JOIN app.bookings b ON ((b.tenant_id = inv.tenant_id) AND (b.booking_id = inv.booking_id))
;

-- end of generated views