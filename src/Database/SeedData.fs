/// Demo seed data — empty arrays used as compile-time type witnesses by the view
/// generator. The generator uses F# quotation analysis (not runtime values) to
/// derive JOIN structure from these array references, so the arrays can be empty.
module Database.SeedData
#if !FABLE_COMPILER

open Shared.Schema.Tables

let partners : Partner array = [||]
let partnerAvailabilities : PartnerAvailability array = [||]
let customers : Customer array = [||]
let services : Service array = [||]
let resources : Resource array = [||]
let serviceResources : ServiceResource array = [||]
let bookings : Booking array = [||]
let invoices : Invoice array = [||]

#endif
