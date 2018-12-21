using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadLengthForCreateFile : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            var uploadDeferLengthHeader = context.Request.GetHeader(HeaderConstants.UploadDeferLength);
            var uploadLengthHeader = context.Request.GetHeader(HeaderConstants.UploadLength);
            return ValidateForPost(context, uploadLengthHeader, uploadDeferLengthHeader);
        }

        private Task ValidateForPost(ContextAdapter context, string uploadLengthHeader, string uploadDeferLengthHeader)
        {
            if (uploadLengthHeader != null && uploadDeferLengthHeader != null)
            {
                return BadRequest(
                    $"Headers {HeaderConstants.UploadLength} and {HeaderConstants.UploadDeferLength} are mutually exclusive and cannot be used in the same request");
            }

            #warning TODO Inject into this class so that we can re-use the model later in the caller?
            var uploadConcat =
                new Models.Concatenation.UploadConcat(context.Request.GetHeader(HeaderConstants.UploadConcat),
                    context.Configuration.UrlPath).Type;

            if (uploadConcat is Models.Concatenation.FileConcatFinal)
            {
                return Done;
            }

            if (uploadDeferLengthHeader == null)
            {
                VerifyRequestUploadLength(context, uploadLengthHeader);
            }
            else
            {
                VerifyDeferLength(uploadDeferLengthHeader);
            }

            return Done;
        }

        private void VerifyDeferLength(string uploadDeferLengthHeader)
        {
            if (uploadDeferLengthHeader != "1")
            {
                BadRequest($"Header {HeaderConstants.UploadDeferLength} must have the value '1' or be omitted");
            }
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

            return Done;
        }
    }
}
