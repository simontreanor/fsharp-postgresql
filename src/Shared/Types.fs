/// Strongly typed ID wrappers that make foreign keys compile-time safe.
/// GuidId<'T>, StringId<'T>, and Int64Id<'T> are distinct types per phantom parameter,
/// so passing the wrong ID type is a compile error rather than a runtime bug.
/// The [<Erase>] attribute compiles each wrapper to its underlying primitive in Fable,
/// giving zero overhead in JavaScript output.
module Shared.Types

open System
open Fable.Core

/// Strongly typed GUID wrapper. Compiles to the underlying Guid in JavaScript.
[<Erase>]
type GuidId<'T> = GuidId of Guid

module GuidId =
    let inline create<'T> id : GuidId<'T> = GuidId id
    let inline empty<'T> : GuidId<'T> = GuidId Guid.Empty
    let inline value(GuidId id) = id

/// Strongly typed Int64 wrapper. Compiles to the underlying int64 in JavaScript.
[<Erase>]
type Int64Id<'T> = Int64Id of int64

module Int64Id =
    let inline create<'T> id : Int64Id<'T> = Int64Id id
    let inline empty<'T> : Int64Id<'T> = Int64Id 0L
    let inline value(Int64Id id) = id

/// Strongly typed String wrapper. Compiles to the underlying string in JavaScript.
[<Erase>]
type StringId<'T> = StringId of string

module StringId =
    let inline create<'T> id : StringId<'T> = StringId id
    let inline empty<'T> : StringId<'T> = StringId ""
    let inline value(StringId id) = id
