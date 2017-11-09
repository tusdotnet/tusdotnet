using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models.Concatenation;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.ProtocolHandlers
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
    internal class HeadHandler : ProtocolMethodHandler
    {
       internal override bool RequiresLock => false;

        internal override Requirement[] Requires => new Requirement[]
        {
            new FileExist(),
            new FileHasNotExpired()
        };

        internal override bool CanHandleRequest(ContextAdapter context)
        {
            return context.UrlMatchesFileIdUrl();
        }

        internal override async Task<bool> Handle(ContextAdapter context)
        {
            var response = context.Response;
            var cancellationToken = context.CancellationToken;
            var store = context.Configuration.Store;

            var fileId = context.GetFileId();

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);

            await AddUploadMetadata(context, fileId, cancellationToken);

            var uploadLength = await store.GetUploadLengthAsync(fileId, cancellationToken);
            AddUploadLength(context, uploadLength);

            var uploadOffset = await store.GetUploadOffsetAsync(fileId, cancellationToken);

            FileConcat uploadConcat = null;
            var addUploadOffset = true;
            if (context.Configuration.Store is ITusConcatenationStore tusConcatStore)
            {
                uploadConcat = await tusConcatStore.GetUploadConcatAsync(fileId, cancellationToken);

                // Only add Upload-Offset to final files if they are complete.
                if (uploadConcat is FileConcatFinal && uploadLength != uploadOffset)
                {
                    addUploadOffset = false;
                }
            }

            if (addUploadOffset)
            {
                response.SetHeader(HeaderConstants.UploadOffset, uploadOffset.ToString());
            }

            if (uploadConcat != null)
            {
                (uploadConcat as FileConcatFinal)?.AddUrlPathToFiles(context.Configuration.UrlPath);
                response.SetHeader(HeaderConstants.UploadConcat, uploadConcat.GetHeader());
            }

            return true;
        }

        private static void AddUploadLength(ContextAdapter context, long? uploadLength)
        {
            if (uploadLength != null)
            {
                context.Response.SetHeader(HeaderConstants.UploadLength, uploadLength.Value.ToString());
            }
            else if (context.Configuration.Store is ITusCreationDeferLengthStore)
            {
                context.Response.SetHeader(HeaderConstants.UploadDeferLength, "1");
            }
        }

        private static Task AddUploadMetadata(ContextAdapter context, string fileId,
            CancellationToken cancellationToken)
        {
            if (!(context.Configuration.Store is ITusCreationStore tusCreationStore))
            {
                return Task.FromResult(0);
            }

            return AddUploadMetadataLocal();

            async Task AddUploadMetadataLocal()
            {
                var uploadMetadata = await tusCreationStore.GetUploadMetadataAsync(fileId, cancellationToken);
                if (!string.IsNullOrEmpty(uploadMetadata))
                {
                    context.Response.SetHeader(HeaderConstants.UploadMetadata, uploadMetadata);
                }
            }
        }
    }
}