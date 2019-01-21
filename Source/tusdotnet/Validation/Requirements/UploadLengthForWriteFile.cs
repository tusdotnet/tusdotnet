using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadLengthForWriteFile : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusCreationDeferLengthStore))
            {
                return TaskHelper.Completed;
            }

            return ValidateAsync(context);
        }

        private async Task ValidateAsync(ContextAdapter context)
        {
            var fileUploadLength = await context.Configuration.Store.GetUploadLengthAsync(context.Request.FileId, context.CancellationToken);

            if (!UploadLengthIsProvidedInRequest(context.Request) && !UploadLengthIsAlreadyPresent(fileUploadLength))
            {
                await BadRequest($"Header {HeaderConstants.UploadLength} must be specified as this file was created using Upload-Defer-Length");
                return;
            }

            if (UploadLengthIsProvidedInRequest(context.Request) && UploadLengthIsAlreadyPresent(fileUploadLength))
            {
                await BadRequest($"{HeaderConstants.UploadLength} cannot be updated once set");
            }
        }

        private static bool UploadLengthIsProvidedInRequest(RequestAdapter request)
        {
            return request.Headers.ContainsKey(HeaderConstants.UploadLength);
        }

        private static bool UploadLengthIsAlreadyPresent(long? uploadLength)
        {
            return uploadLength != null;
        }
    }
}
