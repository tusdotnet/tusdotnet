# tusdotnet - Project Summary

## Overview

tusdotnet is a .NET server implementation of the [tus.io](https://tus.io) resumable upload protocol. It's the most widely used tus implementation on the .NET platform, supporting .NET Framework 4.5.2, .NET Standard 1.3/2.0, .NET Core 3.1, and .NET 6+. The library runs as middleware on both OWIN and ASP.NET Core.

## Architecture

### Core Components

#### 1. Intent Analysis System (`IntentAnalyzer.cs`)

The heart of tusdotnet's request handling is the **intent analysis** pattern. When a request comes in, the `IntentAnalyzer` determines what the client is trying to do based on:

- HTTP method (POST, PATCH, HEAD, DELETE, OPTIONS)
- URL path (whether it matches the tus endpoint or a file-specific URL)
- Request headers (particularly `Tus-Resumable` and `X-HTTP-Method-Override`)

The analyzer supports **multi-intent handling** for the "creation-with-upload" extension, where a single POST request can both create a file AND upload initial data.

#### 2. Intent Handlers (`IntentHandlers/`)

Each tus operation has a dedicated handler:

| Handler | HTTP Method | Purpose |
|---------|-------------|---------|
| `GetOptionsHandler` | OPTIONS | Returns server capabilities (extensions, max size, checksum algorithms) |
| `CreateFileHandler` | POST | Creates a new upload resource |
| `WriteFileHandler` | PATCH | Appends data to an existing upload |
| `GetFileInfoHandler` | HEAD | Returns upload offset, length, and metadata |
| `DeleteFileHandler` | DELETE | Terminates and removes an upload |
| `ConcatenateFilesHandler` | POST | Creates partial or final concatenated files |

#### 3. Store Abstraction (`Interfaces/`)

tusdotnet uses an interface-based approach for data storage, allowing pluggable storage backends:

**Core Interface:**
- `ITusStore` - Basic operations: `AppendDataAsync`, `FileExistAsync`, `GetUploadLengthAsync`, `GetUploadOffsetAsync`

**Extension Interfaces:**
- `ITusCreationStore` - File creation (`CreateFileAsync`, `GetUploadMetadataAsync`)
- `ITusTerminationStore` - File deletion (`DeleteFileAsync`)
- `ITusExpirationStore` - Expiration management (`SetExpirationAsync`, `GetExpirationAsync`, `RemoveExpiredFilesAsync`)
- `ITusChecksumStore` - Checksum verification (`GetSupportedAlgorithmsAsync`, `VerifyChecksumAsync`)
- `ITusConcatenationStore` - File concatenation (`CreatePartialFileAsync`, `CreateFinalFileAsync`, `GetUploadConcatAsync`)
- `ITusCreationDeferLengthStore` - Deferred upload length (`SetUploadLengthAsync`)
- `ITusReadableStore` - Reading completed files (`GetFileAsync`)
- `ITusPipelineStore` - High-performance pipeline support (.NET Core 3.1 / .NET 6+)

#### 4. Built-in Store: `TusDiskStore`

The included `TusDiskStore` saves files to disk and implements ALL extension interfaces. It stores:
- Data file (the actual upload content)
- Metadata files (.uploadlength, .metadata, .uploadconcat, .expiration, etc.)
- Checksum tracking files for chunk verification

#### 5. Validation System (`Validation/`)

A clean requirement-based validation system:

```
Validator → Requirement[] → Each Requirement validates specific aspects
```

Requirements include:
- `FileExist` - Ensures the file exists
- `FileIsNotCompleted` - Prevents writing to completed files
- `ContentType` - Validates `application/offset+octet-stream`
- `UploadOffset` - Validates offset header format
- `RequestOffsetMatchesFileOffset` - Ensures client offset matches server state
- `UploadChecksum` - Validates checksum headers
- `UploadMetadata` - Parses and validates metadata
- `UploadLengthForCreateFileAndConcatenateFiles` - Validates upload length on creation
- `UploadLengthForWriteFile` - Validates upload length during writes
- `UploadConcatForConcatenateFiles` - Validates concatenation headers
- `UploadConcatForWriteFile` - Prevents writes to final concatenated files
- `FileHasNotExpired` - Checks expiration status

#### 6. Configuration (`DefaultTusConfiguration`)

Key configuration options:
- `Store` - The storage backend to use
- `UrlPath` - The URL path for tus endpoints (not used with endpoint routing)
- `MaxAllowedUploadSizeInBytes` / `MaxAllowedUploadSizeInBytesLong` - Maximum upload size (Long takes precedence)
- `Expiration` - Absolute or sliding expiration settings
- `Events` - Callbacks for various lifecycle events
- `AllowedExtensions` - Which tus extensions to enable
- `UsePipelinesIfAvailable` - Enable high-performance pipelines (.NET Core 3.1+ / .NET 6+)
- `ClientReadTimeout` - Timeout for client data reads (default: 60 seconds)
- `FileLockProvider` - Locking mechanism for concurrent access
- `MetadataParsingStrategy` - How to parse metadata (AllowEmptyValues or Original)

#### 7. Adapters (`Adapters/`)

The adapter layer abstracts framework differences (OWIN vs ASP.NET Core):
- `ContextAdapter` - Wraps HttpContext/IOwinContext
- `RequestAdapter` - Unified request access
- `ResponseAdapter` - Unified response handling
- `RequestHeaders` - Type-safe header access

## Protocol Flow

### File Upload Flow

1. **OPTIONS** → Client discovers server capabilities
2. **POST** (to endpoint URL) → Server creates file, returns `Location` header
3. **PATCH** (to file URL) → Client sends data chunks with `Upload-Offset`
4. **HEAD** (to file URL) → Client can check current offset if interrupted
5. **DELETE** (to file URL) → Client can terminate upload (optional)

### Headers Handled

**Request Headers:**
- `Tus-Resumable` - Protocol version
- `X-HTTP-Method-Override` - Method tunneling for restricted environments
- `Upload-Length` - Total file size
- `Upload-Defer-Length` - Deferred length indicator
- `Upload-Offset` - Current upload position
- `Upload-Metadata` - Base64-encoded key-value pairs
- `Upload-Checksum` - Algorithm + hash for verification
- `Upload-Concat` - Concatenation type (partial/final)
- `Content-Type` - Must be `application/offset+octet-stream` for PATCH

**Response Headers:**
- `Tus-Resumable` - Protocol version
- `Tus-Version` - Supported versions
- `Tus-Extension` - Supported extensions
- `Tus-Max-Size` - Maximum upload size
- `Tus-Checksum-Algorithm` - Supported checksum algorithms
- `Upload-Offset` - Current file offset
- `Upload-Length` - Total file size
- `Upload-Expires` - Expiration timestamp
- `Upload-Metadata` - Stored metadata
- `Cache-Control: no-store` - Prevents caching of HEAD responses
- `Location` - URL of created file

## Extensions Supported

| Extension | Description |
|-----------|-------------|
| `creation` | Create new uploads via POST |
| `creation-with-upload` | Include data in initial POST request |
| `creation-defer-length` | Create upload without knowing final size |
| `termination` | Delete uploads via DELETE |
| `checksum` | Verify data integrity per chunk |
| `checksum-trailer` | Send checksum as trailing header (.NET Core 3.1+) |
| `concatenation` | Combine partial uploads into final file |
| `expiration` | Automatic cleanup of incomplete uploads |

## Events System

tusdotnet provides hooks into the upload lifecycle:

- `OnAuthorizeAsync` - Called before processing any request
- `OnBeforeCreateAsync` - Before creating a new upload
- `OnBeforeWriteAsync` - Before writing data (can fire multiple times per upload)
- `OnCreateCompleteAsync` - After successful upload creation
- `OnBeforeDeleteAsync` - Before deleting an upload
- `OnDeleteCompleteAsync` - After successful deletion
- `OnFileCompleteAsync` - When upload is fully complete

## Locking

The library includes a locking mechanism (`ITusFileLockProvider`) to handle concurrent access to the same file. The default implementation uses in-memory locks, but custom implementations can be provided for distributed scenarios.

## Performance Features

- **Pipeline Support** - On .NET Core 3.1+ / .NET 6+, uses `System.IO.Pipelines` for high-performance I/O (enabled by default on .NET 6+)
- **Buffered Reading** - Configurable buffer sizes via `TusDiskBufferSize`
- **Checksum Optimisation** - Pre-calculated checksums stored for faster verification
- **Sliding Expiration** - Efficient tracking of incomplete uploads

## Error Handling

tusdotnet returns appropriate HTTP status codes:

| Code | Meaning |
|------|---------|
| 200 OK | Successful HEAD request |
| 201 Created | Successful file creation |
| 204 No Content | Successful PATCH, DELETE, or OPTIONS |
| 400 Bad Request | Invalid headers or request format |
| 403 Forbidden | Unauthorized or PATCH on final concatenation |
| 404 Not Found | File doesn't exist |
| 409 Conflict | Upload offset mismatch |
| 410 Gone | Expired file |
| 412 Precondition Failed | Unsupported protocol version |
| 413 Request Entity Too Large | File exceeds max size |
| 415 Unsupported Media Type | Wrong Content-Type |
| 460 Checksum Mismatch | Data corruption detected (custom tus code) |
