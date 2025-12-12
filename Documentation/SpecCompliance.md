# tus Protocol Compliance

How well does tusdotnet follow the [tus protocol specification v1.0.0](https://tus.io/protocols/resumable-upload)?

## TL;DR

We're spec-compliant. All the MUST requirements are implemented correctly. The only thing we don't support is `concatenation-unfinished` which is optional and a bit niche.


## Core Protocol

### HEAD Request

Works as expected. Returns the upload offset and length (if known), returns 404 for missing files, and sets `Cache-Control: no-store` to prevent caching.

See `GetFileInfoHandler.cs` for implementation.

---

### PATCH Request

This is where the actual upload happens. We validate:
- Content-Type must be `application/offset+octet-stream` (returns 415 if not)
- Upload-Offset header must match the current file offset (returns 409 if it doesn't)
- Returns 204 on success with the new offset in the response header

The implementation tries to store as much data as possible even if something goes wrong partway through.

See `WriteFileHandler.cs` and `RequestOffsetMatchesFileOffset.cs`.

---

### OPTIONS Request

Returns capabilities - what extensions are enabled, max file size, supported checksum algorithms, etc. The spec wants 204 or 200, we return 204.

One quirk: we only return `Tus-Version: 1.0.0` because that's all we support. The spec example shows multiple versions like `1.0.0,0.2.2,0.2.1` but we're not implementing ancient tus versions, so there's no point.

See `GetOptionsHandler.cs`.

---

### Headers & Version Handling

All the standard headers work correctly:
- Upload-Offset, Upload-Length: validated as non-negative integers
- Tus-Resumable: required on every request except OPTIONS
- X-HTTP-Method-Override: supported for clients that can't send certain HTTP methods

If a client sends an unsupported tus version, we return 412 Precondition Failed with the version we support.

See `HeaderConstants.cs` and `IntentAnalyzer.cs`.


## Extensions

### Creation

POST to create a new upload. You must provide either `Upload-Length` or `Upload-Defer-Length: 1` (not both). We'll return 413 if the file is too big, 201 Created if everything's fine.

The Location header in the response tells you where to upload the file.

Upload-Metadata is supported - send metadata as base64-encoded key-value pairs, and we'll return it in HEAD responses.

Empty files (Upload-Length: 0) work fine.

See `CreateFileHandler.cs`.

---

### Creation With Upload

You can include data in the POST request body when creating the file. Saves a round-trip.

Content-Type must be `application/offset+octet-stream`, same as PATCH. The response includes Upload-Offset to tell you how much was written.

See `IntentAnalyzer.cs` and `WriteFileContextForCreationWithUpload.cs`.

---

### Expiration

Files can expire. If configured, we include `Upload-Expires` in responses (formatted as RFC 1123 datetime).

Expired files return 410 Gone.

We support both absolute expiration (file expires at a fixed time) and sliding expiration (timer resets on each upload). The spec only requires the basic version but sliding expiration is more useful in practice.

See `ExpirationHelper.cs` and `ITusExpirationStore.cs`.

---

### Checksum

Prevents corrupted uploads. We support SHA1 (required by spec) plus optionally MD5, SHA256, SHA384, SHA512.

If the checksum doesn't match, we return 460 Checksum Mismatch and discard the chunk.

On .NET Core 3.1+ we also support checksums sent as HTTP trailers, which is useful for clients that compute the checksum while streaming.

See `ChecksumHelper.cs` and `ChecksumTrailerHelper.cs`.

---

### Termination

DELETE requests to remove an upload and free storage. Returns 204 on success, 404 if it's already gone.

See `DeleteFileHandler.cs`.

---

### Concatenation

Lets you upload a file in parallel chunks and then concatenate them on the server.

First, create partial uploads with `Upload-Concat: partial`. Then create a final upload with `Upload-Concat: final;/files/abc /files/def` listing the partials to combine.

The final file's length equals the sum of the partial files. You can't PATCH a final file (returns 403).

**What we don't support:** `concatenation-unfinished` - the ability to concatenate partial files that aren't fully uploaded yet. The spec makes this optional and it's not commonly used, so we just require that partials are complete before concatenation.

See `ConcatenateFilesHandler.cs`.

---

### Creation-Defer-Length

Lets you create a file without knowing the length upfront. Send `Upload-Defer-Length: 1` in the POST request, then include `Upload-Length` in a later PATCH request to set the length.

Once set, the length can't change.

See `ITusCreationDeferLengthStore.cs` and `UploadLengthForWriteFile.cs`.


## HTTP Status Codes

Here's what gets returned and when:

- **200 OK** - HEAD requests
- **201 Created** - POST creates a file
- **204 No Content** - PATCH/DELETE/OPTIONS success
- **400 Bad Request** - Malformed headers, invalid values
- **403 Forbidden** - Trying to PATCH a final concatenated file, auth failures
- **404 Not Found** - File doesn't exist
- **409 Conflict** - Upload-Offset doesn't match server's current offset
- **410 Gone** - File expired
- **412 Precondition Failed** - Tus version not supported
- **413 Request Entity Too Large** - File exceeds max size
- **415 Unsupported Media Type** - Wrong Content-Type header
- **460 Checksum Mismatch** - Data corruption detected

---

## Beyond the Spec

Some things we do that aren't in the spec but are useful:

- Configurable storage backends - plug in your own store implementation for disk, Azure Blob Storage, AWS S3, database, or whatever you need
- Extensions can be enabled/disabled per configuration
- Lenient metadata parsing (in addition to strict parsing)
- Sliding expiration mode
- Event hooks (OnBeforeCreate, OnFileComplete, OnAuthorize, etc.)
- File locking to prevent concurrent writes
- Pipeline support for high-performance I/O on .NET Core 3.1+
- Pluggable file ID providers

---

## Summary

All the MUST requirements are implemented. The SHOULDs are mostly implemented (we picked reasonable defaults where there were choices). The only optional feature we skip is `concatenation-unfinished`.

If you need a tus server for .NET, this'll do the job.
