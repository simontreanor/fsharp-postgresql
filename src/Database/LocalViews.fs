/// F# quotation-based view definitions. Each view is expressed as a typed lambda
/// that the generator translates to SQL JOIN logic. The compiler catches field-reference
/// mistakes before any SQL is emitted.
module Database.LocalViews
#if !FABLE_COMPILER

open Shared.Schema.Tables
open Shared.Schema.Views

open Database.SeedData

/// Booking joined with its partner.
/// Demonstrates a simple two-table join (booking -> partner) using Array.filter -> Array.map.
let bookingDetailView =
    <@
    fun (b: Booking) ->
        partners
        |> Array.filter(fun p -> p.PartnerId = b.PartnerId)
        |> Array.map(fun p ->
            {
                TenantId = b.TenantId
                BookingId = b.BookingId
                PartnerName = p.Name
                CustomerId = b.CustomerId
                ServiceId = b.ServiceId
                ResourceId = b.ResourceId
                Status = b.Status
                Notes = b.Notes
            }
        )
    @>

/// ServiceResource joined to services and resources.
/// Shows filtering through a join table to find all resources available for each service.
let serviceAvailabilityView =
    <@
    fun (sr: ServiceResource) ->
        services
        |> Array.filter(fun s -> s.ServiceId = sr.ServiceId)
        |> Array.collect(fun s ->
            resources
            |> Array.filter(fun r -> r.ResourceId = sr.ResourceId)
            |> Array.map(fun r ->
                {
                    ServiceId = s.ServiceId
                    ServiceName = s.Name
                    ResourceId = r.ResourceId
                    ResourceName = r.Name
                }
            )
        )
    @>

/// Invoice joined to booking. Simpler two-table join for comparison.
let invoiceDetailView =
    <@
    fun (inv: Invoice) ->
        bookings
        |> Array.filter(fun b -> b.TenantId = inv.TenantId && b.BookingId = inv.BookingId)
        |> Array.map(fun b ->
            {
                TenantId = inv.TenantId
                InvoiceId = inv.InvoiceId
                BookingId = b.BookingId
                Amount = inv.Amount
                IssuedAt = inv.IssuedAt
            }
        )
    @>

#endif
