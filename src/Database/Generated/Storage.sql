-- generated storage SQL (2026-06-04 22:22:25 UTC)

-- documents storage bucket
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'documents',
  'documents',
  false,
  10485760, -- 10 MB
  ARRAY['application/pdf', 'image/jpeg', 'image/png', 'image/webp']
)
ON CONFLICT (id) DO UPDATE SET
  file_size_limit    = EXCLUDED.file_size_limit,
  allowed_mime_types = EXCLUDED.allowed_mime_types;

-- public images storage bucket
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'images',
  'images',
  true,
  5242880, -- 5 MB
  ARRAY['image/jpeg', 'image/png', 'image/webp', 'image/gif']
)
ON CONFLICT (id) DO UPDATE SET
  file_size_limit    = EXCLUDED.file_size_limit,
  allowed_mime_types = EXCLUDED.allowed_mime_types;

-- Partners can upload to their own documents folder
CREATE POLICY "Partners upload own documents" ON storage.objects
FOR INSERT TO authenticated
WITH CHECK (
  bucket_id = 'documents'
  AND
(storage.foldername(name))[1]::uuid = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
  AND
  (storage.foldername(name))[2]::uuid IN (
    SELECT partner_id FROM app.get_my_partner_ids()
  )
);

-- Partners can read from their own documents folder
CREATE POLICY "Partners read own documents" ON storage.objects
FOR SELECT TO authenticated
USING (
  bucket_id = 'documents'
  AND
(storage.foldername(name))[1]::uuid = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
  AND
  (storage.foldername(name))[2]::uuid IN (
    SELECT partner_id FROM app.get_my_partner_ids()
  )
);

-- Partners can delete their own documents
CREATE POLICY "Partners delete own documents" ON storage.objects
FOR DELETE TO authenticated
USING (
  bucket_id = 'documents'
  AND
(storage.foldername(name))[1]::uuid = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
  AND
  (storage.foldername(name))[2]::uuid IN (
    SELECT partner_id FROM app.get_my_partner_ids()
  )
);

-- Anonymous users can read public images
CREATE POLICY "Public image read access" ON storage.objects
FOR SELECT TO anon, authenticated
USING (bucket_id = 'images');

-- Authenticated users in a tenant can upload to the tenant's image folder
CREATE POLICY "Tenant image upload" ON storage.objects
FOR INSERT TO authenticated
WITH CHECK (
  bucket_id = 'images' AND
(storage.foldername(name))[1]::uuid = (auth.jwt() -> 'app_metadata' ->> 'tenant_id')::uuid
);

-- end of storage SQL