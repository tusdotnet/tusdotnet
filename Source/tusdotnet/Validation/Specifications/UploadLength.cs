using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;

namespace tusdotnet.Validation.Specifications
{
    internal class UploadLength : Specification
    {
        public override async Task Validate(ContextAdapter context)
        {
            var uploadDeferLengthHeader = context.Request.GetHeader(HeaderConstants.UploadDeferLength);
            var uploadLengthHeader = context.Request.GetHeader(HeaderConstants.UploadLength);

            if (context.Request.GetMethod().Equals("post", StringComparison.OrdinalIgnoreCase))
            {
                await ValidateForPost(context, uploadLengthHeader, uploadDeferLengthHeader);
            }
            else
            {
                await ValidateForPatch(context);
            }
        }

        private async Task ValidateForPatch(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusCreationDeferLengthStore))
            {
                return;
            }

            var fileId = context.GetFileId();

            var fileUploadLength = await context.Configuration.Store.GetUploadLengthAsync(fileId, context.CancellationToken);

            if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadLength) && fileUploadLength == null)
            {
                await BadRequest(
                    $"Header {HeaderConstants.UploadLength} must be specified as this file was created using Upload-Defer-Length");
            }
            else
            {
                if (context.Request.Headers.ContainsKey(HeaderConstants.UploadLength) && fileUploadLength != null)
                {
                    await BadRequest($"{HeaderConstants.UploadLength} cannot be updated once set");
                }
            }
        }

        private Task ValidateForPost(ContextAdapter context, string uploadLengthHeader, string uploadDeferLengthHeader)
        {
            if (uploadLengthHeader != null && uploadDeferLengthHeader != null)
            {
                BadRequest(
                    $"Headers {HeaderConstants.UploadLength} and {HeaderConstants.UploadDeferLength} are mutually exclusive and cannot be used in the same request");
                return Done;
            }

            var uploadConcat =
                new Models.Concatenation.UploadConcat(context.Request.GetHeader(HeaderConstants.UploadConcat),
                    context.Configuration.UrlPath).Type;

            if (uploadConcat is Models.Concatenation.FileConcatFinal)
            {
                return Done;
            }

            if (uploadDeferLengthHeader == null)
            {
                VerifyRequestUploadLengthAsync(context, uploadLengthHeader);
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

        private void VerifyRequestUploadLengthAsync(ContextAdapter context, string uploadLengthHeader)
        {
            var request = context.Request;
            if (uploadLengthHeader == null)
            {
                BadRequest($"Missing {HeaderConstants.UploadLength} header");
                return;
            }

            if (!long.TryParse(request.Headers[HeaderConstants.UploadLength].First(), out long uploadLength))
            {
                BadRequest($"Could not parse {HeaderConstants.UploadLength}");
                return;
            }

            if (uploadLength < 0)
            {
                BadRequest($"Header {HeaderConstants.UploadLength} must be a positive number");
                return;
            }

            if (uploadLength > context.Configuration.MaxAllowedUploadSizeInBytes)
            {
                StatusCode = HttpStatusCode.RequestEntityTooLarge;
                ErrorMessage = $"Header {HeaderConstants.UploadLength} exceeds the server's max file size.";
            }
        }
    }
}
