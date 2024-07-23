using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Threading.Tasks;

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
            context.SetHeader("Upload-Complete", UploadComplete.ToSfBool());

            if (ResourceWasJustCreated)
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
