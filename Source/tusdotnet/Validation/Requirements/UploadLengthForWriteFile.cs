using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadLengthForWriteFile : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            var uploadLengthIsSet = await UploadLengthIsAlreadyPresent(context);
            var supportsUploadDeferLength = context.Configuration.Store is ITusCreationDeferLengthStore;

            if (!supportsUploadDeferLength && !uploadLengthIsSet)
            {
                throw new TusConfigurationException($"File {context.Request.FileId} does not have an upload length and the current store ({context.Configuration.Store.GetType().FullName}) does not support Upload-Defer-Length so no new upload length can be set");
            }

            if (!UploadLengthIsProvidedInRequest(context.Request) && !uploadLengthIsSet)
            {
                await BadRequest($"Header {HeaderConstants.UploadLength} must be specified as this file was created using Upload-Defer-Length");
                return;
            }

            if (UploadLengthIsProvidedInRequest(context.Request) && uploadLengthIsSet)
            {
                await BadRequest($"{HeaderConstants.UploadLength} cannot be updated once set");
            }
        }

        private static async Task<bool> UploadLengthIsAlreadyPresent(ContextAdapter context)
        {
            var fileUploadLength = await context.Configuration.Store.GetUploadLengthAsync(context.Request.FileId, context.CancellationToken);
            return fileUploadLength != null;
        }

        private static bool UploadLengthIsProvidedInRequest(RequestAdapter request)
        {
            return request.Headers.ContainsKey(HeaderConstants.UploadLength);
        }
    }
}
