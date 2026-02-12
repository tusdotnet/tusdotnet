#nullable enable
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Models;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadLengthForWriteFile : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            var fileUploadLength = await context.StoreAdapter.GetUploadLengthAsync(
                context.FileId,
                context.CancellationToken
            );

            var uploadLengthIsSet = fileUploadLength != null;
            var supportsUploadDeferLength = context.StoreAdapter.Extensions.CreationDeferLength;

            if (!supportsUploadDeferLength && !uploadLengthIsSet)
            {
                throw new TusConfigurationException(
                    $"File {context.FileId} does not have an upload length and the current store ({context.Configuration.Store.GetType().FullName}) does not support Upload-Defer-Length so no new upload length can be set"
                );
            }

            if (!UploadLengthIsProvidedInRequest(context.Request))
            {
                return;
            }

            if (uploadLengthIsSet)
            {
                await BadRequest($"{HeaderConstants.UploadLength} cannot be updated once set");
                return;
            }

            await VerifyRequestUploadLength(context);
        }

        private static bool UploadLengthIsProvidedInRequest(RequestAdapter request)
        {
            return request.Headers.ContainsKey(HeaderConstants.UploadLength);
        }

        private Task VerifyRequestUploadLength(ContextAdapter context)
        {
            var request = context.Request;

            if (
                !long.TryParse(request.Headers[HeaderConstants.UploadLength], out long uploadLength)
            )
            {
                return BadRequest($"Could not parse {HeaderConstants.UploadLength}");
            }

            if (uploadLength < 0)
            {
                return BadRequest(
                    $"Header {HeaderConstants.UploadLength} must be a positive number"
                );
            }

            if (uploadLength > context.Configuration.GetMaxAllowedUploadSizeInBytes())
            {
                return RequestEntityTooLarge(
                    $"Header {HeaderConstants.UploadLength} exceeds the server's max file size."
                );
            }

            return TaskHelper.Completed;
        }
    }
}
