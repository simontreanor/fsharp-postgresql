/// Custom attributes for database schema mapping.
/// Attach these to record fields to declare primary keys, foreign keys,
/// unique constraints, defaults, and exclusion constraints.
/// The generator reads them via reflection to emit PostgreSQL DDL.
module Shared.Attributes

open System

[<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
type PKAttribute() =
    inherit Attribute()

[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type UniqueAttribute(group: string) =
    inherit Attribute()
    member this.Group = group

[<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
type DefaultAttribute(defaultValue: string) =
    inherit Attribute()
    member this.DefaultValue = defaultValue

/// Foreign key to another table. When multiple columns on the same record each carry
/// [<FK(typeof<T>)>], they are grouped together as a composite foreign key to T.
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type FKAttribute(tableType: Type, prefix: string) =
    inherit Attribute()
    new(tableType: Type) = FKAttribute(tableType, "")
    member this.TableType = tableType
    member this.Prefix = prefix

/// Marks columns that participate in an exclusion constraint (EXCLUDE USING gist).
/// timestamptz columns become the range part: tstzrange(col_a, col_b) WITH &&
/// All other columns become equality parts: col WITH =
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
type WithoutOverlapAttribute() =
    inherit Attribute()

[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
type ViewAttribute() =
    inherit Attribute()
