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

        internal override async Task Invoke()
        {
#warning TODO: TypedResponseHeaders? TusResumable could be set for all responses
            // headers.TusResumable = HeaderConstants.TusResumableValue
            // headers.CacheControl = HeaderConstants.NoStore;
            // headers.UploadMetadata = metadata
            // etc
            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);

            var uploadMetadata = await GetMetadata(Context);
            if (!string.IsNullOrEmpty(uploadMetadata))
            {
                Response.SetHeader(HeaderConstants.UploadMetadata, uploadMetadata);
            }
            
            var uploadLength = await Store.GetUploadLengthAsync(Request.FileId, CancellationToken);
            AddUploadLength(Context, uploadLength);

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
        }

        private Task<string> GetMetadata(ContextAdapter context)
        {
            if (context.Configuration.Store is ITusCreationStore tusCreationStore)
                return tusCreationStore.GetUploadMetadataAsync(Request.FileId, context.CancellationToken);

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
