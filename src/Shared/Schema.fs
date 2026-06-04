/// Demo schema module. This is the single source of truth for table shape,
/// keys, constraints, and RLS intent. The generator reads these types via
/// reflection and emits PostgreSQL DDL from them.
module Shared.Schema

open System
open Fable.Core
open Attributes
open Shared.Types

module Enums =

    [<StringEnum(CaseRules.SnakeCase)>]
    type BookingStatus =
        | Draft
        | Pending
        | Confirmed
        | Cancelled
        | Completed

    [<StringEnum(CaseRules.SnakeCase)>]
    type ResourceType =
        | ConferenceRoom
        | Equipment
        | ProcessingCenter

module Tables =

    open Enums

    type Tenant = {
        [<PK>]
        TenantId: GuidId<Tenant>
        Name: string
    }

    type Partner = {
        [<PK>]
        PartnerId: StringId<Partner>
        [<FK(typeof<Tenant>); Unique("partner_name_unique_within_tenant")>]
        TenantId: GuidId<Tenant>
        [<Unique("partner_name_unique_within_tenant")>]
        Name: string
        [<Default("'draft'")>]
        Status: BookingStatus
    }

    /// Availability windows for a partner. The [<WithoutOverlap>] attribute on the
    /// UUID columns (equality) and timestamptz columns (range) generates an exclusion
    /// constraint: EXCLUDE USING gist (window_id WITH =, tstzrange(slot_start, slot_end) WITH &&)
    type PartnerAvailability = {
        [<PK>]
        WindowId: GuidId<PartnerAvailability>
        [<FK(typeof<Partner>)>]
        PartnerId: StringId<Partner>
        [<WithoutOverlap>]
        SlotStart: DateTimeOffset
        [<WithoutOverlap>]
        SlotEnd: DateTimeOffset
    }

    type Customer = {
        [<PK; FK(typeof<Tenant>)>]
        TenantId: GuidId<Tenant>
        [<PK>]
        CustomerId: GuidId<Customer>
        Name: string
        Email: string
    }

    /// Service definition with StringId<'T> phantom type for compile-time safety.
    /// Example: StringId<Service>.create "dermatology-consult"
    /// Cannot be confused with StringId<Resource> at compile time.
    /// ServiceId is the primary key; services are scoped to a tenant via tenant_id.
    type Service = {
        [<PK>]
        ServiceId: StringId<Service>
        [<FK(typeof<Tenant>)>]
        TenantId: GuidId<Tenant>
        Name: string
        /// [<Default>] provides a SQL default; application still needs to handle optional fields
        [<Default("60")>]
        DurationMinutes: int
    }

    /// Resource (room, equipment, processing center).
    /// StringId<Resource> is the primary key; resources are scoped to a tenant and partner.
    type Resource = {
        [<PK>]
        ResourceId: StringId<Resource>
        [<FK(typeof<Tenant>)>]
        TenantId: GuidId<Tenant>
        [<FK(typeof<Partner>)>]
        PartnerId: StringId<Partner>
        Name: string
        Type: ResourceType
    }

    /// Join table linking services to resources. Both ServiceId and ResourceId
    /// are phantom-typed StringIds, preventing compile-time type confusion.
    /// Multi-column unique constraint on (service_id, resource_id, tenant_id)
    /// prevents duplicate service-resource allocations within a tenant.
    [<Literal>]
    let private ServiceResourceKey = "service_resource_unique"

    type ServiceResource = {
        [<PK>]
        Id: GuidId<ServiceResource>
        [<FK(typeof<Service>); Unique(ServiceResourceKey)>]
        ServiceId: StringId<Service>
        [<FK(typeof<Resource>); Unique(ServiceResourceKey)>]
        ResourceId: StringId<Resource>
        [<FK(typeof<Tenant>); Unique(ServiceResourceKey)>]
        TenantId: GuidId<Tenant>
    }

    /// Booking ties together customer, partner, service, and resource.
    /// Service and resource IDs are phantom-typed StringIds to prevent compile-time confusion.
    type Booking = {
        [<PK>]
        BookingId: GuidId<Booking>
        [<FK(typeof<Tenant>); FK(typeof<Customer>)>]
        TenantId: GuidId<Tenant>
        [<FK(typeof<Partner>)>]
        PartnerId: StringId<Partner>
        [<FK(typeof<Customer>)>]
        CustomerId: GuidId<Customer>
        [<FK(typeof<Service>)>]
        ServiceId: StringId<Service>
        [<FK(typeof<Resource>)>]
        ResourceId: StringId<Resource>
        [<Default("'draft'")>]
        Status: BookingStatus
        Notes: string option
    }

    /// Invoice with Int64Id<'T> phantom type. Sequential bigint allows natural sorting.
    /// Example: Int64Id<Invoice>.create 1001L
    type Invoice = {
        [<PK; FK(typeof<Tenant>)>]
        TenantId: GuidId<Tenant>
        [<PK>]
        InvoiceId: Int64Id<Invoice>
        [<FK(typeof<Booking>)>]
        BookingId: GuidId<Booking>
        Amount: decimal
        [<Default("now()")>]
        IssuedAt: DateTimeOffset
    }

module Views =

    open Tables
    open Enums

    /// Booking with partner details. Simple two-table join.
    type BookingDetail = {
        TenantId: GuidId<Tenant>
        BookingId: GuidId<Booking>
        PartnerName: string
        CustomerId: GuidId<Customer>
        ServiceId: StringId<Service>
        ResourceId: StringId<Resource>
        Status: BookingStatus
        Notes: string option
    }

    /// Service paired with its available resources. Shows filtering and joining
    /// through a join table (ServiceResource).
    type ServiceAvailability = {
        ServiceId: StringId<Service>
        ServiceName: string
        ResourceId: StringId<Resource>
        ResourceName: string
    }

    /// Invoice joined to booking for display. Demonstrates simpler two-table join.
    type InvoiceDetail = {
        TenantId: GuidId<Tenant>
        InvoiceId: Int64Id<Invoice>
        BookingId: GuidId<Booking>
        Amount: decimal
        IssuedAt: DateTimeOffset
    }
