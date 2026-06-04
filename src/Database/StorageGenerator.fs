/// Storage bucket and RLS policy generator.
/// Defines buckets with size limits and MIME type restrictions, and generates
/// storage.objects RLS policies to control upload and download access.
module Database.StorageGenerator
#if !FABLE_COMPILER

open System
open Shared.Config

// ── Bucket definitions ────────────────────────────────────────────────────────

/// Documents bucket: 10 MB limit, PDF and image files only.
let private documentsBucket () = [|
    "-- documents storage bucket"
    "INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)"
    "VALUES ("
    "  'documents',"
    "  'documents',"
    "  false,"
    "  10485760, -- 10 MB"
    "  ARRAY['application/pdf', 'image/jpeg', 'image/png', 'image/webp']"
    ")"
    "ON CONFLICT (id) DO UPDATE SET"
    "  file_size_limit    = EXCLUDED.file_size_limit,"
    "  allowed_mime_types = EXCLUDED.allowed_mime_types;"
|]

/// Public images bucket: 5 MB limit, images only, publicly readable.
let private imagesBucket () = [|
    "-- public images storage bucket"
    "INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)"
    "VALUES ("
    "  'images',"
    "  'images',"
    "  true,"
    "  5242880, -- 5 MB"
    "  ARRAY['image/jpeg', 'image/png', 'image/webp', 'image/gif']"
    ")"
    "ON CONFLICT (id) DO UPDATE SET"
    "  file_size_limit    = EXCLUDED.file_size_limit,"
    "  allowed_mime_types = EXCLUDED.allowed_mime_types;"
|]

// ── Storage RLS helpers ───────────────────────────────────────────────────────

/// Checks that the JWT tenant_id claim matches the first path segment of the object.
/// Folder convention: tenant_id/partner_id/filename
let private tenantCheck =
    "(storage.foldername(name))[1]::uuid = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid"

/// Checks that the current user belongs to the partner whose ID is the second path segment.
let private partnerCheck =
    $"  (storage.foldername(name))[2]::uuid IN (\n    SELECT partner_id FROM {SCHEMA_NAME}.get_my_partner_ids()\n  )"

// ── Storage RLS policies ──────────────────────────────────────────────────────

let private documentsPolicies () =
    let condition =
        [
            "  bucket_id = 'documents'"
            tenantCheck
            partnerCheck
        ]
        |> String.concat "\n  AND\n"

    [|
        "-- Partners can upload to their own documents folder"
        $"""CREATE POLICY "Partners upload own documents" ON storage.objects"""
        "FOR INSERT TO authenticated"
        "WITH CHECK ("
        condition
        ");"
        ""
        "-- Partners can read from their own documents folder"
        $"""CREATE POLICY "Partners read own documents" ON storage.objects"""
        "FOR SELECT TO authenticated"
        "USING ("
        condition
        ");"
        ""
        "-- Partners can delete their own documents"
        $"""CREATE POLICY "Partners delete own documents" ON storage.objects"""
        "FOR DELETE TO authenticated"
        "USING ("
        condition
        ");"
    |]

let private imagesPolicies () =
    [|
        "-- Anonymous users can read public images"
        $"""CREATE POLICY "Public image read access" ON storage.objects"""
        "FOR SELECT TO anon, authenticated"
        "USING (bucket_id = 'images');"
        ""
        "-- Authenticated users in a tenant can upload to the tenant's image folder"
        $"""CREATE POLICY "Tenant image upload" ON storage.objects"""
        "FOR INSERT TO authenticated"
        "WITH CHECK ("
        "  bucket_id = 'images' AND"
        tenantCheck
        ");"
    |]

// ── Entry point ───────────────────────────────────────────────────────────────

let generateAllStorage() =
    [|
        yield $"-- generated storage SQL ({DateTimeOffset.UtcNow:``yyyy-MM-dd HH:mm:ss``} UTC)"
        yield ""
        yield! documentsBucket()
        yield ""
        yield! imagesBucket()
        yield ""
        yield! documentsPolicies()
        yield ""
        yield! imagesPolicies()
        yield ""
        yield "-- end of storage SQL"
    |]
    |> String.concat "\n"

#endif
