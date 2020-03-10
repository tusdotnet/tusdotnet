using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadLengthForCreateFileAndConcatenateFiles : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            var uploadDeferLengthHeader = context.Request.GetHeader(HeaderConstants.UploadDeferLength);
            var uploadLengthHeader = context.Request.GetHeader(HeaderConstants.UploadLength);

            if (uploadLengthHeader != null && uploadDeferLengthHeader != null)
            {
                return BadRequest($"Headers {HeaderConstants.UploadLength} and {HeaderConstants.UploadDeferLength} are mutually exclusive and cannot be used in the same request");
            }

            if (uploadDeferLengthHeader == null)
            {
                return VerifyRequestUploadLength(context, uploadLengthHeader);
            }

            var deferLengthStore = context.Configuration.Store as ITusCreationDeferLengthStore;
            return VerifyDeferLength(deferLengthStore, uploadDeferLengthHeader);
        }

        private Task VerifyDeferLength(ITusCreationDeferLengthStore deferLengthStore, string uploadDeferLengthHeader)
        {
            if (deferLengthStore == null)
            {
                return BadRequest($"Header {HeaderConstants.UploadDeferLength} is not supported");
            }

            if (uploadDeferLengthHeader != "1")
            {
                return BadRequest($"Header {HeaderConstants.UploadDeferLength} must have the value '1' or be omitted");
            }

            return TaskHelper.Completed;
        }

        private Task VerifyRequestUploadLength(ContextAdapter context, string uploadLengthHeader)
        {
            var request = context.Request;
            if (uploadLengthHeader == null)
            {
                return BadRequest($"Missing {HeaderConstants.UploadLength} header");
            }

            if (!long.TryParse(request.Headers[HeaderConstants.UploadLength][0], out long uploadLength))
            {
                return BadRequest($"Could not parse {HeaderConstants.UploadLength}");
            }

            if (uploadLength < 0)
            {
                return BadRequest($"Header {HeaderConstants.UploadLength} must be a positive number");
            }

            if (uploadLength > context.Configuration.GetMaxAllowedUploadSizeInBytes())
            {
                return RequestEntityTooLarge(
                    $"Header {HeaderConstants.UploadLength} exceeds the server's max file size.");
            }

            return TaskHelper.Completed;
        }
    }
}
