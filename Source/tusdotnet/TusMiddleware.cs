using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Expiration;

namespace tusdotnet
{
    internal static class TusMiddleware
    {
        public static async Task<bool> Invoke(ContextAdapter context)
        {
            context.Configuration.Validate();

            var request = context.Request;
            var response = context.Response;

            if (!ShouldHandleRequest(context))
            {
                return false;
            }

            var method = request.GetMethod();

            var tusResumable = request.Headers.ContainsKey(HeaderConstants.TusResumable)
                ? request.Headers[HeaderConstants.TusResumable].FirstOrDefault()
                : null;

            if (method != "options" && tusResumable != HeaderConstants.TusResumableValue)
            {
                response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
                response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);
                return await response.Error(HttpStatusCode.PreconditionFailed,
                    $"Tus version {tusResumable} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");
            }

            switch (method)
            {
                case "post":
                    return await HandlePostRequest(context);
                case "head":
                    return await HandleHeadRequest(context);
                case "patch":
                    return await HandlePatchRequest(context);
                case "options":
                    return await HandleOptionsRequest(context);
                case "delete":
                    return await HandleDeleteRequest(context);
                default:
                    throw new NotImplementedException($"HTTP method {method} is not implemented.");
            }
        }

        private static async Task<bool> HandleDeleteRequest(ContextAdapter context)
        {
            /* When receiving a DELETE request for an existing upload the Server SHOULD free associated resources and MUST 
			 * respond with the 204 No Content status confirming that the upload was terminated. 
			 * For all future requests to this URL the Server SHOULD respond with the 404 Not Found or 410 Gone status.
			 * */

            var store = context.Configuration.Store as ITusTerminationStore;
            if (store == null)
            {
                return false;
            }

            var response = context.Response;
            var cancellationToken = context.CancellationToken;

            var fileId = context.GetFileId();
            var fileLock = new FileLock(fileId);

            var hasLock = fileLock.Lock(cancellationToken);
            if (!hasLock)
            {
                return await response.Error(HttpStatusCode.Conflict,
                    $"File {fileId} is currently being updated. Please try again later");
            }

            try
            {
                var exists = await context.Configuration.Store.FileExistAsync(fileId, cancellationToken);
                if (!exists)
                {
                    response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
                    response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
                    response.SetStatus((int)HttpStatusCode.NotFound);
                    return true;
                }

                if (store is ITusExpirationStore expirationStore)
                {
                    var expires = await expirationStore.GetExpirationAsync(fileId, cancellationToken);
                    if (expires?.HasPassed() == true)
                    {
                        return response.NotFound();
                    }
                }

                await store.DeleteFileAsync(fileId, cancellationToken);
                response.SetStatus((int)HttpStatusCode.NoContent);
                response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            }
            finally
            {
                fileLock.ReleaseIfHeld();
            }

            return true;
        }

        private static async Task<bool> HandlePostRequest(ContextAdapter context)
        {
            /*
			 * Creation:
			 * The Client MUST send a POST request against a known upload creation URL to request a new upload resource. 
			 * The request MUST include one of the following headers:
			 * a) Upload-Length to indicate the size of an entire upload in bytes.
			 * b) Upload-Defer-Length: 1 if upload size is not known at the time. 
			 * Once it is known the Client MUST set the Upload-Length header in the next PATCH request. 
			 * Once set the length MUST NOT be changed. As long as the length of the upload is not known, t
			 * he Server MUST set Upload-Defer-Length: 1 in all responses to HEAD requests.
			 * If the Server supports deferring length, it MUST add creation-defer-length to the Tus-Extension header.
			 * The Client MAY supply the Upload-Metadata header to add additional metadata to the upload creation request. 
			 * The Server MAY decide to ignore or use this information to further process the request or to reject it. 
			 * If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata 
			 * header and its value as specified by the Client during the creation.
			 * If the length of the upload exceeds the maximum, which MAY be specified using the Tus-Max-Size header, 
			 * the Server MUST respond with the 413 Request Entity Too Large status.
			 * The Server MUST acknowledge a successful upload creation with the 201 Created status. 
			 * The Server MUST set the Location header to the URL of the created resource. This URL MAY be absolute or relative.
			 * The Client MUST perform the actual upload using the core protocol.
			 * 
			 * Concatenation:
			 * This extension can be used to concatenate multiple uploads into a single one enabling Clients to perform parallel uploads and 
			 * to upload non-contiguous chunks. If the Server supports this extension, it MUST add concatenation to the Tus-Extension header.
			 * A partial upload represents a chunk of a file. It is constructed by including the Upload-Concat: partial header 
			 * while creating a new upload using the Creation extension. Multiple partial uploads are concatenated into a 
			 * final upload in the specified order. The Server SHOULD NOT process these partial uploads until they are 
			 * concatenated to form a final upload. The length of the final upload MUST be the sum of the length of all partial uploads.
			 * In order to create a new final upload the Client MUST add the Upload-Concat header to the upload creation request. 
			 * The value MUST be final followed by a semicolon and a space-separated list of the partial upload URLs that need to be concatenated. 
			 * The partial uploads MUST be concatenated as per the order specified in the list. 
			 * This concatenation request SHOULD happen after all of the corresponding partial uploads are completed.
			 * The Client MUST NOT include the Upload-Length header in the final upload creation.
			 * The Client MAY send the concatenation request while the partial uploads are still in progress.
			 * This feature MUST be explicitly announced by the Server by adding concatenation-unfinished to the Tus-Extension header.
			 * When creating a new final upload the partial uploads’ metadata SHALL NOT be transferred to the new final upload.
			 * All metadata SHOULD be included in the concatenation request using the Upload-Metadata header.
			 * The Server MAY delete partial uploads after concatenation. They MAY however be used multiple times to form a final resource.
             * 
             * Expiration:
             * If the expiration is known at the creation, the Upload-Expires header MUST be included in the response to the initial POST request. 
			 */

            var tusCreationStore = context.Configuration.Store as ITusCreationStore;
            if (tusCreationStore == null)
            {
                return false;
            }

            var request = context.Request;
            var response = context.Response;
            var cancellationToken = context.CancellationToken;

            var tusConcatenationStore = context.Configuration.Store as ITusConcatenationStore;
            UploadConcat uploadConcat = null;
            if (tusConcatenationStore != null && request.Headers.ContainsKey(HeaderConstants.UploadConcat))
            {
                uploadConcat = new UploadConcat(request.Headers[HeaderConstants.UploadConcat].First(), context.Configuration.UrlPath);
                if (!uploadConcat.IsValid)
                {
                    return await response.Error( HttpStatusCode.BadRequest, uploadConcat.ErrorMessage);
                }
            }

            var uploadLength = -1L;
            if (!(uploadConcat?.Type is FileConcatFinal))
            {
                var requestLengthResult = await VerifyRequestFileLength(context);
                uploadLength = requestLengthResult.Item2;
                if (requestLengthResult.Item1)
                {
                    return true;
                }
            }

            string metadata = null;
            if (request.Headers.ContainsKey(HeaderConstants.UploadMetadata))
            {
                var validateMetadataResult =
                    Metadata.ValidateMetadataHeader(request.Headers[HeaderConstants.UploadMetadata].First());
                if (!string.IsNullOrEmpty(validateMetadataResult))
                {
                    return await response.Error( HttpStatusCode.BadRequest, validateMetadataResult);
                }

                metadata = request.Headers[HeaderConstants.UploadMetadata].FirstOrDefault();
            }

            string fileId = null;
            DateTimeOffset? expires = null;

            try
            {
                if (tusConcatenationStore != null && uploadConcat != null)
                {
                    if (uploadConcat.Type is FileConcatPartial)
                    {
                        fileId = await tusConcatenationStore
                                .CreatePartialFileAsync(uploadLength, metadata, cancellationToken);

                    }
                    else if (uploadConcat.Type is FileConcatFinal finalConcat)
                    {
                        var filesExist =
                                await Task.WhenAll(finalConcat.Files.Select(f => context.Configuration.Store.FileExistAsync(f, cancellationToken)));

                        if (filesExist.Any(f => !f))
                        {
                            return await response.Error(
                                HttpStatusCode.BadRequest,
                                $"Could not find some of the files supplied for concatenation: {string.Join(", ", filesExist.Zip(finalConcat.Files, (b, s) => new { exist = b, name = s }).Where(f => !f.exist).Select(f => f.name))}");
                        }

                        var filesArePartial = await Task.WhenAll(finalConcat.Files.Select(f =>
                            tusConcatenationStore.GetUploadConcatAsync(f, cancellationToken)));

                        if (filesArePartial.Any(f => !(f is FileConcatPartial)))
                        {
                            return await response.Error(
                                HttpStatusCode.BadRequest,
                                $"Some of the files supplied for concatenation are not marked as partial and can not be concatenated: {string.Join(", ", filesArePartial.Zip(finalConcat.Files, (s, s1) => new { partial = s is FileConcatPartial, name = s1 }).Where(f => !f.partial).Select(f => f.name))}"
                            );
                        }

                        var incompleteFiles = new List<string>();
                        var totalSize = 0L;
                        foreach (var file in finalConcat.Files)
                        {
                            var length = context.Configuration.Store.GetUploadLengthAsync(file, cancellationToken);
                            var offset = context.Configuration.Store.GetUploadOffsetAsync(file, cancellationToken);
                            await Task.WhenAll(length, offset);

                            if (length.Result != null)
                            {
                                totalSize += length.Result.Value;
                            }

                            if (length.Result != offset.Result)
                            {
                                incompleteFiles.Add(file);
                            }
                        }

                        if (incompleteFiles.Any())
                        {
                            return await response.Error(
                                HttpStatusCode.BadRequest,
                                $"Some of the files supplied for concatenation are not finished and can not be concatenated: {string.Join(", ", incompleteFiles)}");
                        }

                        if (totalSize > context.Configuration.MaxAllowedUploadSizeInBytes)
                        {
                            return await response.Error(HttpStatusCode.RequestEntityTooLarge,
                                "The concatenated file exceeds the server's max file size.");
                        }

                        fileId = await tusConcatenationStore.CreateFinalFileAsync(finalConcat.Files, metadata,
                            cancellationToken);

                        // Run callback that the final file is completed.
                        if (context.Configuration.OnUploadCompleteAsync != null)
                        {
                            await context.Configuration.OnUploadCompleteAsync(fileId, context.Configuration.Store, cancellationToken);
                        }
                    }
                }
                else
                {
                    fileId = await tusCreationStore.CreateFileAsync(uploadLength, metadata, cancellationToken);
                }

                if (context.Configuration.Store is ITusExpirationStore expirationStore
                    && context.Configuration.Expiration != null
                    && !(uploadConcat?.Type is FileConcatFinal))
                {
                    expires = DateTimeOffset.UtcNow.Add(context.Configuration.Expiration.Timeout);
                    await expirationStore.SetExpirationAsync(fileId, expires.Value, context.CancellationToken);
                }
            }
            catch (TusStoreException storeException)
            {
                return await response.Error( HttpStatusCode.BadRequest, storeException.Message);
            }

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.Location, $"{context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");

            if (expires != null)
            {
                response.SetHeader(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
            }

            response.SetStatus((int)HttpStatusCode.Created);
            return true;
        }

        private static async Task<Tuple<bool, long>> VerifyRequestFileLength(ContextAdapter context)
        {
            var request = context.Request;
            var response = context.Response;
            var uploadLength = -1L;
            var handledInvalid = new Tuple<bool, long>(true, -1);
            var uploadDeferLengthHeader = request.GetHeader(HeaderConstants.UploadDeferLength);
            var uploadLengthHeader = request.GetHeader(HeaderConstants.UploadLength);

            if (uploadLengthHeader != null && uploadDeferLengthHeader != null)
            {
                await response.Error(HttpStatusCode.BadRequest,
                    $"Headers {HeaderConstants.UploadLength} and {HeaderConstants.UploadDeferLength} are mutually exclusive and cannot be used in the same request");
                return handledInvalid;
            }

            if (uploadDeferLengthHeader == null)
            {
                var uploadLengthErrorMessage = VerifyRequestUploadLengthAsync(context, uploadLengthHeader);
                if (uploadLengthErrorMessage.Item1 == HttpStatusCode.OK)
                {
                    uploadLength = uploadLengthErrorMessage.Item3;
                }
                else
                {
                    await response.Error(uploadLengthErrorMessage.Item1, uploadLengthErrorMessage.Item2);
                    return handledInvalid;
                }
            }
            else
            {
                if (uploadDeferLengthHeader != "1")
                {
                    return new Tuple<bool, long>(
                        await response.Error(HttpStatusCode.BadRequest,
                            $"Header {HeaderConstants.UploadDeferLength} must have the value '1' or be omitted"),
                        -1
                    );
                }
            }

            return new Tuple<bool, long>(false, uploadLength);
        }

        private static Tuple<HttpStatusCode, string, long> VerifyRequestUploadLengthAsync(ContextAdapter context, string uploadLengthHeader)
        {
            var request = context.Request;
            if (uploadLengthHeader == null)
            {
                return new Tuple<HttpStatusCode, string, long>(HttpStatusCode.BadRequest,
                    $"Missing {HeaderConstants.UploadLength} header", -1);
            }

            if (!long.TryParse(request.Headers[HeaderConstants.UploadLength].First(), out long uploadLength))
            {
                return new Tuple<HttpStatusCode, string, long>(HttpStatusCode.BadRequest,
                    $"Could not parse {HeaderConstants.UploadLength}", -1);
            }

            if (uploadLength < 0)
            {
                return new Tuple<HttpStatusCode, string, long>(HttpStatusCode.BadRequest,
                    $"Header {HeaderConstants.UploadLength} must be a positive number", -1);
            }

            if (uploadLength > context.Configuration.MaxAllowedUploadSizeInBytes)
            {
                return new Tuple<HttpStatusCode, string, long>(HttpStatusCode.RequestEntityTooLarge,
                    $"Header {HeaderConstants.UploadLength} exceeds the server's max file size.", -1);
            }

            return new Tuple<HttpStatusCode, string, long>(HttpStatusCode.OK, null, uploadLength);
        }

        private static async Task<bool> HandleOptionsRequest(ContextAdapter context)
        {
            /*
			 * An OPTIONS request MAY be used to gather information about the Server’s current configuration. 
			 * A successful response indicated by the 204 No Content status MUST contain the Tus-Version header. 
			 * It MAY include the Tus-Extension and Tus-Max-Size headers.
			 * The Client SHOULD NOT include the Tus-Resumable header in the request and the Server MUST discard it.
			 * */

            var response = context.Response;
            var cancellationToken = context.CancellationToken;

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);

            if (context.Configuration.MaxAllowedUploadSizeInBytes.HasValue)
            {
                response.SetHeader(HeaderConstants.TusMaxSize, context.Configuration.MaxAllowedUploadSizeInBytes.Value.ToString());
            }

            var extensions = context.DetectExtensions();
            if (extensions.Any())
            {
                response.SetHeader(HeaderConstants.TusExtension, string.Join(",", extensions));
            }

            if (context.Configuration.Store is ITusChecksumStore checksumStore)
            {
                var checksumAlgorithms = await checksumStore.GetSupportedAlgorithmsAsync(cancellationToken);
                response.SetHeader(HeaderConstants.TusChecksumAlgorithm, string.Join(",", checksumAlgorithms));
            }

            response.SetStatus((int)HttpStatusCode.NoContent);
            return true;
        }

        private static async Task<bool> HandlePatchRequest(ContextAdapter context)
        {
            /*
			 * The Server SHOULD accept PATCH requests against any upload URL and apply the bytes 
			 * contained in the message at the given offset specified by the Upload-Offset header. 
			 * All PATCH requests MUST use Content-Type: application/offset+octet-stream.
			 * The Upload-Offset header’s value MUST be equal to the current offset of the resource. 
			 * In order to achieve parallel upload the Concatenation extension MAY be used. 
			 * If the offsets do not match, the Server MUST respond with the 409 Conflict status without modifying the upload resource.
			 * The Client SHOULD send all the remaining bytes of an upload in a single PATCH request, 
			 * but MAY also use multiple small requests successively for scenarios where 
			 * this is desirable, for example, if the Checksum extension is used.
			 * The Server MUST acknowledge successful PATCH requests with the 204 No Content status. 
			 * It MUST include the Upload-Offset header containing the new offset. 
			 * The new offset MUST be the sum of the offset before the PATCH request and the number of bytes received and 
			 * processed or stored during the current PATCH request.
			 * Both, Client and Server, SHOULD attempt to detect and handle network errors predictably. 
			 * They MAY do so by checking for read/write socket errors, as well as setting read/write timeouts. 
			 * A timeout SHOULD be handled by closing the underlying connection.
			 * The Server SHOULD always attempt to store as much of the received data as possible.
			 * 
			 * Concatenation:
			 * The Server MUST respond with the 403 Forbidden status to PATCH requests against a final upload URL and 
			 * MUST NOT modify the final or its partial uploads.
             * 
             * Expiration:
             * [Upload-Expires] This header MUST be included in every PATCH response if the upload is going to expire. 
			 * */

            var request = context.Request;
            var response = context.Response;
            var cancellationToken = context.CancellationToken;

            var fileId = context.GetFileId();
            var fileLock = new FileLock(fileId);
            var checksumStore = context.Configuration.Store as ITusChecksumStore;
            var providedChecksum = request.Headers.ContainsKey(HeaderConstants.UploadChecksum)
                ? new Checksum(request.Headers[HeaderConstants.UploadChecksum].First())
                : null;

            var expirationStore = context.Configuration.Store as ITusExpirationStore;

            var hasLock = fileLock.Lock(cancellationToken);

            if (!hasLock)
            {
                return await response.Error(HttpStatusCode.Conflict,
                        $"File {fileId} is currently being updated. Please try again later");
            }

            try
            {
                var concatStore = context.Configuration.Store as ITusConcatenationStore;
                var creationDeferLengthStore = context.Configuration.Store as ITusCreationDeferLengthStore;

                FileConcat uploadConcat = null;
                if (concatStore != null)
                {
                    uploadConcat = await concatStore.GetUploadConcatAsync(fileId, cancellationToken);

                    if (uploadConcat is FileConcatFinal)
                    {
                        return await response.Error( HttpStatusCode.Forbidden, "File with \"Upload-Concat: final\" cannot be patched");
                    }
                }

                if (request.ContentType == null ||
                    !request.ContentType.Equals("application/offset+octet-stream",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return await response.Error(
                        HttpStatusCode.BadRequest,
                        $"Content-Type {request.ContentType} is invalid. Must be application/offset+octet-stream");
                }

                var fileUploadLength = await context.Configuration.Store.GetUploadLengthAsync(fileId, cancellationToken);

                if (creationDeferLengthStore != null)
                {
                    if (!request.Headers.ContainsKey(HeaderConstants.UploadLength) && fileUploadLength == null)
                    {
                        return await response.Error(HttpStatusCode.BadRequest,
                            $"Header {HeaderConstants.UploadLength} must be specified as this file was created using Upload-Defer-Length");
                    }
                    else
                    {
                        if (request.Headers.ContainsKey(HeaderConstants.UploadLength) && fileUploadLength != null)
                        {
                            return await response.Error(HttpStatusCode.BadRequest,
                                $"{HeaderConstants.UploadLength} cannot be updated once set");
                        }
                    }
                }

                if (!request.Headers.ContainsKey(HeaderConstants.UploadOffset))
                {
                    return await response.Error( HttpStatusCode.BadRequest, $"Missing {HeaderConstants.UploadOffset} header");
                }


                if (!long.TryParse(request.Headers[HeaderConstants.UploadOffset].FirstOrDefault(), out long requestOffset))
                {
                    return await response.Error( HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadOffset} header");
                }

                if (requestOffset < 0)
                {
                    return await response.Error( HttpStatusCode.BadRequest,
                        $"Header {HeaderConstants.UploadOffset} must be a positive number");
                }

                if (checksumStore != null && providedChecksum != null)
                {
                    if (!providedChecksum.IsValid)
                    {
                        return await response.Error( HttpStatusCode.BadRequest, $"Could not parse {HeaderConstants.UploadChecksum} header");
                    }

                    var checksumAlgorithms = (await checksumStore.GetSupportedAlgorithmsAsync(cancellationToken)).ToList();
                    if (!checksumAlgorithms.Contains(providedChecksum.Algorithm))
                    {
                        return await response.Error( HttpStatusCode.BadRequest,
                            $"Unsupported checksum algorithm. Supported algorithms are: {string.Join(",", checksumAlgorithms)}");
                    }
                }

                var exists = await context.Configuration.Store.FileExistAsync(fileId, cancellationToken);
                if (!exists)
                {
                    return response.NotFound();
                }

                DateTimeOffset? expires = null;

                if (expirationStore != null)
                {
                    expires = await expirationStore.GetExpirationAsync(fileId, cancellationToken);
                    if (expires?.HasPassed() == true)
                    {
                        return response.NotFound();
                    }
                }

                var fileOffset = await context.Configuration.Store.GetUploadOffsetAsync(fileId, cancellationToken);

                if (requestOffset != fileOffset)
                {
                    return await response.Error(
                        HttpStatusCode.Conflict,
                        $"Offset does not match file. File offset: {fileOffset}. Request offset: {requestOffset}");
                }

                if (fileUploadLength != null && fileOffset == fileUploadLength.Value)
                {
                    return await response.Error( HttpStatusCode.BadRequest, "Upload is already complete.");
                }

                if (creationDeferLengthStore != null && request.Headers.ContainsKey(HeaderConstants.UploadLength))
                {
                    var uploadLengthResult =
                        VerifyRequestUploadLengthAsync(context, request.GetHeader(HeaderConstants.UploadLength));

                    if (uploadLengthResult.Item1 == HttpStatusCode.OK)
                    {
                        await creationDeferLengthStore.SetUploadLengthAsync(fileId, uploadLengthResult.Item3,
                            cancellationToken);
                    }
                }

                long bytesWritten;
                try
                {
                    bytesWritten = await context.Configuration.Store.AppendDataAsync(fileId, request.Body, cancellationToken);
                }
                catch (IOException ioException)
                {
                    // Indicates that the client disconnected.
                    if (!ioException.ClientDisconnected())
                    {
                        throw;
                    }

                    // Client disconnected so no need to return a response.
                    return true;
                }
                catch (TusStoreException storeException)
                {
                    await response.Error( HttpStatusCode.BadRequest, storeException.Message);
                    throw;
                }
                finally
                {
                    if (expirationStore != null && context.Configuration.Expiration is SlidingExpiration slidingExpiration)
                    {
                        expires = DateTimeOffset.UtcNow.Add(slidingExpiration.Timeout);
                        await expirationStore.SetExpirationAsync(fileId, expires.Value, cancellationToken);
                    }
                }

                if (checksumStore != null && providedChecksum != null)
                {
                    var validChecksum = await checksumStore
                        .VerifyChecksumAsync(fileId, providedChecksum.Algorithm, providedChecksum.Hash, cancellationToken);

                    if (!validChecksum)
                    {
                        return await response.Error( (HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");
                    }
                }

                response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
                response.SetHeader(HeaderConstants.UploadOffset, (fileOffset + bytesWritten).ToString());

                if (expires.HasValue)
                {
                    response.SetHeader(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
                }

                response.SetStatus((int)HttpStatusCode.NoContent);

                // Run OnUploadComplete if it has been provided.
                var fileIsComplete = fileUploadLength != null && (fileOffset + bytesWritten) == fileUploadLength.Value;
                if (fileIsComplete && !(uploadConcat is FileConcatPartial) && context.Configuration.OnUploadCompleteAsync != null)
                {
                    await context.Configuration.OnUploadCompleteAsync(fileId, context.Configuration.Store, cancellationToken);
                }
            }
            finally
            {
                fileLock.ReleaseIfHeld();
            }

            return true;
        }

        private static async Task<bool> HandleHeadRequest(ContextAdapter context)
        {
            /*
			 * The Server MUST always include the Upload-Offset header in the response for a HEAD request, 
			 * even if the offset is 0, or the upload is already considered completed. If the size of the upload is known, 
			 * the Server MUST include the Upload-Length header in the response. 
			 * If the resource is not found, the Server SHOULD return either the 404 Not Found, 410 Gone or 403 Forbidden 
			 * status without the Upload-Offset header.
			 * The Server MUST prevent the client and/or proxies from caching the response by adding the 
			 * Cache-Control: no-store header to the response.
			 * 
			 * If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata header 
			 * and its value as specified by the Client during the creation.
			 * 
			 * Concatenation:
			 * The response to a HEAD request for a final upload SHOULD NOT contain the Upload-Offset header unless the 
			 * concatenation has been successfully finished. After successful concatenation, the Upload-Offset and Upload-Length 
			 * MUST be set and their values MUST be equal. The value of the Upload-Offset header before concatenation is not 
			 * defined for a final upload. The response to a HEAD request for a partial upload MUST contain the Upload-Offset header.
			 * The Upload-Length header MUST be included if the length of the final resource can be calculated at the time of the request. 
			 * Response to HEAD request against partial or final upload MUST include the Upload-Concat header and its value as received 
			 * in the upload creation request.
			 */

            var response = context.Response;
            var cancellationToken = context.CancellationToken;

            var fileId = context.GetFileId();

            var exists = await context.Configuration.Store.FileExistAsync(fileId, cancellationToken);
            if (!exists)
            {
                return response.NotFound();
            }

            DateTimeOffset? expires = null;
            if (context.Configuration.Store is ITusExpirationStore expirationStore)
            {
                expires = await expirationStore.GetExpirationAsync(fileId, cancellationToken);
            }

            if (expires?.HasPassed() == true)
            {
                return response.NotFound();
            }

            var uploadLength = await context.Configuration.Store.GetUploadLengthAsync(fileId, cancellationToken);
            if (uploadLength != null)
            {
                    response.SetHeader(HeaderConstants.UploadLength, uploadLength.Value.ToString());
            }
            else if (context.Configuration.Store is ITusCreationDeferLengthStore)
            {
                response.SetHeader(HeaderConstants.UploadDeferLength, "1");
            }

            if (context.Configuration.Store is ITusCreationStore tusCreationStore)
            {
                var uploadMetadata = await tusCreationStore.GetUploadMetadataAsync(fileId, cancellationToken);
                if (!string.IsNullOrEmpty(uploadMetadata))
                {
                    response.SetHeader(HeaderConstants.UploadMetadata, uploadMetadata);
                }
            }

            var uploadOffset = await context.Configuration.Store.GetUploadOffsetAsync(fileId, cancellationToken);

            var tusConcatStore = context.Configuration.Store as ITusConcatenationStore;
            FileConcat uploadConcat = null;
            var addUploadOffset = true;
            if (tusConcatStore != null)
            {
                uploadConcat = await tusConcatStore.GetUploadConcatAsync(fileId, cancellationToken);

                // Only add Upload-Offset to final files if they are complete.
                if (uploadConcat is FileConcatFinal && uploadLength != uploadOffset)
                {
                    addUploadOffset = false;
                }
            }

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            if (addUploadOffset)
            {
                response.SetHeader(HeaderConstants.UploadOffset, uploadOffset.ToString());
            }
            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
            if (uploadConcat != null)
            {
                (uploadConcat as FileConcatFinal)?.AddUrlPathToFiles(context.Configuration.UrlPath);
                response.SetHeader(HeaderConstants.UploadConcat, uploadConcat.GetHeader());
            }

            return true;
        }

        private static bool ShouldHandleRequest(ContextAdapter context)
        {
            var request = context.Request;
            var method = request.GetMethod();

            if (!request.Headers.ContainsKey(HeaderConstants.TusResumable) && method != "options")
            {
                return false;
            }

            switch (method)
            {
                case "post":
                case "options":
                    return context.IsExactUrlMatch();
                case "head":
                case "patch":
                case "delete":
                    return !context.IsExactUrlMatch() &&
                           request.RequestUri.LocalPath.StartsWith(context.Configuration.UrlPath, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }
    }
}