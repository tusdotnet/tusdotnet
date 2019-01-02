using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
{
    internal class GetFileInfoHandler : IntentHandler
    {
        internal override Requirement[] Requires => new Requirement[]
        {
            new FileExist(),
            new FileHasNotExpired()
        };

        public GetFileInfoHandler(ContextAdapter context) 
            : base(context, IntentType.GetFileInfo, LockType.NoLock)
        {
        }

        internal override async Task<ResultType> Invoke()
        {
            var response = Context.Response;
            var cancellationToken = Context.CancellationToken;
            var store = Context.Configuration.Store;

#warning TODO: TypedResponseHeaders? TusResumable could be set for all responses
            // headers.TusResumable = HeaderConstants.TusResumableValue
            // headers.CacheControl = HeaderConstants.NoStore;
            // headers.UploadMetadata = metadata
            // etc
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);

            var uploadMetadata = await GetMetadata(Context);
            if (!string.IsNullOrEmpty(uploadMetadata))
            {
                response.SetHeader(HeaderConstants.UploadMetadata, uploadMetadata);
            }
            
            var uploadLength = await store.GetUploadLengthAsync(Context.RequestFileId, cancellationToken);
            AddUploadLength(Context, uploadLength);

            var uploadOffset = await store.GetUploadOffsetAsync(Context.RequestFileId, cancellationToken);

            FileConcat uploadConcat = null;
            var addUploadOffset = true;
            if (Context.Configuration.Store is ITusConcatenationStore tusConcatStore)
            {
                uploadConcat = await tusConcatStore.GetUploadConcatAsync(Context.RequestFileId, cancellationToken);

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
                (uploadConcat as FileConcatFinal)?.AddUrlPathToFiles(Context.Configuration.UrlPath);
                response.SetHeader(HeaderConstants.UploadConcat, uploadConcat.GetHeader());
            }
            #warning TODO Do we need to return anthing from the handlers? Seems to always be stop execution
            return ResultType.StopExecution;
        }

        private Task<string> GetMetadata(ContextAdapter context)
        {
            if (context.Configuration.Store is ITusCreationStore tusCreationStore)
                return tusCreationStore.GetUploadMetadataAsync(Context.RequestFileId, context.CancellationToken);

            return Task.FromResult<string>(null);
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
    }
}
