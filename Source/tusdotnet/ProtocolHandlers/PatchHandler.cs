using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Configuration.tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.ProtocolHandlers
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
    */
    internal class PatchHandler : ProtocolMethodHandler
    {
        internal override bool RequiresLock => true;

        internal override Requirement[] Requires => new Requirement[]
        {
            new Validation.Requirements.UploadConcat(),
            new ContentType(),
            new UploadLength(),
            new UploadOffset(),
            new UploadChecksum(),
            new FileExist(),
            new FileHasNotExpired(),
            new RequestOffsetMatchesFileOffset(),
            new FileIsNotCompleted()
        };

        internal override bool CanHandleRequest(ContextAdapter context)
        {
            return context.UrlMatchesFileIdUrl();
        }

        internal override async Task<bool> Handle(ContextAdapter context)
        {
            var creationDeferLengthStore = context.Configuration.Store as ITusCreationDeferLengthStore;

            var fileId = context.GetFileId();
            var cancellationToken = context.CancellationToken;
            var response = context.Response;

            if (creationDeferLengthStore != null && context.Request.Headers.ContainsKey(HeaderConstants.UploadLength))
            {
                var uploadLength = long.Parse(context.Request.GetHeader(HeaderConstants.UploadLength));
                await creationDeferLengthStore.SetUploadLengthAsync(fileId, uploadLength, cancellationToken);
            }

            DateTimeOffset? expires;
            long bytesWritten;
            try
            {
                bytesWritten = await context.Configuration.Store.AppendDataAsync(fileId, context.Request.Body, cancellationToken);
            }
            // Client disconnected so no need to return a response.
            catch (Exception exception) when (ClientDisconnected(exception))
            {
                return true;
            }
            finally
            {
                expires = await GetOrUpdateExpires(context);
            }

            if (!await MatchChecksum(context))
            {
                return await response.Error((HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");
            }

            var fileOffset = long.Parse(context.Request.GetHeader(HeaderConstants.UploadOffset));

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.UploadOffset, (fileOffset + bytesWritten).ToString());

            if (expires.HasValue)
            {
                response.SetHeader(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
            }

            response.SetStatus((int)HttpStatusCode.NoContent);

            // Run OnUploadComplete if it has been provided.
            await RunOnUploadComplete(context, fileOffset, bytesWritten);
            return true;

            bool ClientDisconnected(Exception exception)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    return true;
                }

                // IsCancellationRequested is false when connecting directly to Kestrel. Instead the exception below is thrown.
                return exception.GetType().FullName == "Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException";
            }
        }

        private static async Task RunOnUploadComplete(ContextAdapter context, long fileOffset, long bytesWritten)
        {
            if (context.Configuration.OnUploadCompleteAsync == null && context.Configuration.Events?.OnFileCompleteAsync == null)
            {
                return;
            }

            if (await IsPartialUpload(context))
            {
                return;
            }

            var fileId = context.GetFileId();
            var fileUploadLength = await context.Configuration.Store.GetUploadLengthAsync(fileId, context.CancellationToken);
            var fileIsComplete = fileOffset + bytesWritten == fileUploadLength;

            if (fileIsComplete)
            {
                if(context.Configuration.OnUploadCompleteAsync != null)
                {
                    await context.Configuration.OnUploadCompleteAsync(fileId, context.Configuration.Store, context.CancellationToken);
                }

                if (context.Configuration.Events?.OnFileCompleteAsync != null)
                {
                    await context.Configuration.Events.OnFileCompleteAsync(
                        EventContext.FromContext<FileCompleteContext>(context));
                }
            }
        }

        private static async Task<bool> IsPartialUpload(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusConcatenationStore concatenationStore))
            {
                return false;
            }

            var concat = await concatenationStore.GetUploadConcatAsync(context.GetFileId(), context.CancellationToken);

            return concat is FileConcatPartial;
        }

        private static async Task<bool> MatchChecksum(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusChecksumStore checksumStore))
            {
                return true;
            }

            var checksumHeader = context.Request.GetHeader(HeaderConstants.UploadChecksum);

            if (checksumHeader == null)
            {
                return true;
            }

            var providedChecksum = new Checksum(checksumHeader);

            return await checksumStore.VerifyChecksumAsync(context.GetFileId(), providedChecksum.Algorithm,
                providedChecksum.Hash, context.CancellationToken);
        }

        private static async Task<DateTimeOffset?> GetOrUpdateExpires(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusExpirationStore expirationStore))
            {
                return null;
            }

            var fileId = context.GetFileId();

            if (context.Configuration.Expiration is SlidingExpiration slidingExpiration)
            {
                var expires = DateTimeOffset.UtcNow.Add(slidingExpiration.Timeout);
                await expirationStore.SetExpirationAsync(fileId, expires, context.CancellationToken);
                return expires;
            }

            return await expirationStore.GetExpirationAsync(fileId, context.CancellationToken);
        }
    }
}