using System;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadLength : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            if (context.Request.GetMethod().Equals("post", StringComparison.OrdinalIgnoreCase))
            {
                var uploadDeferLengthHeader = context.Request.GetHeader(HeaderConstants.UploadDeferLength);
                var uploadLengthHeader = context.Request.GetHeader(HeaderConstants.UploadLength);
                await ValidateForPost(context, uploadLengthHeader, uploadDeferLengthHeader);
            }
            else
            {
                await ValidateForPatch(context);
            }
        }

        private Task ValidateForPatch(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusCreationDeferLengthStore))
            {
                return Task.FromResult(0);
            }

            return ValidateForPatchLocal();

            async Task ValidateForPatchLocal()
            {
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
        }

        private Task ValidateForPost(ContextAdapter context, string uploadLengthHeader, string uploadDeferLengthHeader)
        {
            if (uploadLengthHeader != null && uploadDeferLengthHeader != null)
            {
                return BadRequest(
                    $"Headers {HeaderConstants.UploadLength} and {HeaderConstants.UploadDeferLength} are mutually exclusive and cannot be used in the same request");
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

            if (uploadLength > context.Configuration.MaxAllowedUploadSizeInBytes)
            {
                return RequestEntityTooLarge(
                    $"Header {HeaderConstants.UploadLength} exceeds the server's max file size.");
            }

            return Done;
        }
    }
}
