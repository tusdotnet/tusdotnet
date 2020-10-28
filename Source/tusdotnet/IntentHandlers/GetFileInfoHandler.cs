using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Extensions.Internal;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Parsers;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
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
    * The response to a HEAD request for a final upload SHOULD NOT contain the Upload-Offset header unless the 
    * concatenation has been successfully finished. After successful concatenation, the Upload-Offset and Upload-Length 
    * MUST be set and their values MUST be equal. The value of the Upload-Offset header before concatenation is not 
    * defined for a final upload. The response to a HEAD request for a partial upload MUST contain the Upload-Offset header.
    * The Upload-Length header MUST be included if the length of the final resource can be calculated at the time of the request. 
    * Response to HEAD request against partial or final upload MUST include the Upload-Concat header and its value as received 
    * in the upload creation request.
    */

    internal class GetFileInfoHandler : IntentHandler
    {
        internal override Requirement[] Requires => new Requirement[]
        {
            new ClientTagForHead(),
            new FileExist(),
            new FileHasNotExpired()
        };

        public GetFileInfoHandler(ContextAdapter context)
            : base(context, IntentType.GetFileInfo, LockType.NoLock)
        {
        }

        internal override async Task<ResultType> Challenge(UploadChallengeParserResult uploadChallenge, ITusChallengeStoreHashFunction hashFunction, ITusChallengeStore challengeStore)
        {
            var fileId = Context.Request.FileId;
            var uploadTag = Context.Request.GetHeader(HeaderConstants.UploadTag);

            if (string.IsNullOrEmpty(fileId))
            {
                if (Context.Configuration.SupportsClientTag() && !string.IsNullOrEmpty(uploadTag))
                {
                    var fileIdMap = await ((ITusClientTagStore)Context.Configuration.Store).ResolveUploadTagToFileIdAsync(uploadTag);
                    if (fileIdMap == null)
                    {
                        Context.Response.NotFound();
                        return ResultType.StopExecution;
                    }

                    // TODO: Cache this through the request somehow
                    fileId = fileIdMap.FileId;
                }
                else
                {
                    Context.Response.NotFound();
                    return ResultType.StopExecution;
                }
            }
            var secret = await challengeStore.GetUploadSecretAsync(fileId, Context.CancellationToken);

            if (string.IsNullOrEmpty(secret))
                return ResultType.ContinueExecution;

            if (!uploadChallenge.AssertUploadChallengeIsProvidedIfSecretIsSet(secret))
            {
                Context.Response.NotFound();
                return ResultType.StopExecution;
            }

            if (!uploadChallenge.VerifyChecksum(Context.Request.GetHeader(HeaderConstants.UploadOffset), Context.Request.GetHttpMethod(), secret, hashFunction))
            {
                Context.Response.NotFound();
                return ResultType.StopExecution;
            }

            Context.Request.UploadChallengeProvidedAndPassed = true;
            return ResultType.ContinueExecution;
        }

        internal override async Task Invoke()
        {
            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);

            var uploadMetadata = await GetMetadata(Context);
            if (!string.IsNullOrEmpty(uploadMetadata))
            {
                Response.SetHeader(HeaderConstants.UploadMetadata, uploadMetadata);
            }

            var uploadLength = await Store.GetUploadLengthAsync(Request.FileId, CancellationToken);
            SetUploadLengthHeader(Context, uploadLength);

            var uploadOffset = await Store.GetUploadOffsetAsync(Request.FileId, CancellationToken);

            FileConcat uploadConcat = null;
            var addUploadOffset = true;
            if (Context.Configuration.Store is ITusConcatenationStore tusConcatStore)
            {
                uploadConcat = await tusConcatStore.GetUploadConcatAsync(Request.FileId, CancellationToken);

                // Only add Upload-Offset to final files if they are complete.
                if (uploadConcat is FileConcatFinal && uploadLength != uploadOffset)
                {
                    addUploadOffset = false;
                }
            }

            if (addUploadOffset)
            {
                Response.SetHeader(HeaderConstants.UploadOffset, uploadOffset.ToString());
            }

            if (uploadConcat != null)
            {
                (uploadConcat as FileConcatFinal)?.AddUrlPathToFiles(Context.Configuration.UrlPath);
                Response.SetHeader(HeaderConstants.UploadConcat, uploadConcat.GetHeader());
            }

            if (Context.Configuration.SupportsClientTag())
            {
                // TODO: Only add location if upload-tag was used (not if directly for a file resource)
                Response.SetHeader(HeaderConstants.Location, Context.CreateLocationHeaderValue(Request.FileId));
            }
        }

        private Task<string> GetMetadata(ContextAdapter context)
        {
            if (context.Configuration.Store is ITusCreationStore tusCreationStore)
                return tusCreationStore.GetUploadMetadataAsync(Request.FileId, context.CancellationToken);

            return Task.FromResult<string>(null);
        }

        private static void SetUploadLengthHeader(ContextAdapter context, long? uploadLength)
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
    }
}
