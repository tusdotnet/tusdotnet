using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace tusdotnet.Tus2
{
    public class UploadTransferProcedureResponse : Tus2BaseResponse
    {
        internal string ResourceId { get; set; }

        internal bool UploadComplete { get; set; }

        internal bool ResourceWasJustCreated { get; set; }

        internal TusHandlerLimits Limits { get; set; }

        public Uri? ContentLocation { get; set; }

        protected override Task WriteResponse(HttpContext context)
        {
            // Append should add this header so that the client can determine if the failure is due to the upload or post processing
            if (!ResourceWasJustCreated)
            {
                context.SetHeader("Upload-Complete", UploadComplete.ToSfBool());
            }

            // Add these if this is a creation call and the entire upload was not completed.
            // I.e.:
            // Upload-Complete: ?0 -> add headers
            // Upload-Complete: ?1 -> add headers if the entire upload was not complete. This is basically a noop as the client would have disconnected here.
            if (ResourceWasJustCreated && !UploadComplete)
            {
                var displayUrl = context.Request.GetDisplayUrl().TrimEnd('/');
                context.SetHeader("Location", displayUrl + "/" + ResourceId);

                if (Limits is not null)
                    context.SetHeader("Upload-Limit", Limits.ToSfDictionary());
            }

            if (ContentLocation is not null)
                context.SetHeader("Content-Location", ContentLocation.ToString());

            return Task.CompletedTask;
        }
    }
}
