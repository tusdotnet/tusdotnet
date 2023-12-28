using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class UploadTransferProcedureResponse : Tus2BaseResponse
    {
        internal string ResourceId { get; set; }

        internal bool RequestUploadIncomplete { get; set; }

        internal bool ResourceWasJustCreated { get; set; }

        public bool EntireUploadCompleted { get; internal set; }

        public Uri? ContentLocation { get; set; }

        protected override Task WriteResponse(HttpContext context)
        {
            if (!EntireUploadCompleted)
                context.SetHeader("Upload-Incomplete", RequestUploadIncomplete.ToSfBool());

            if (ResourceWasJustCreated)
            {
                var displayUrl = context.Request.GetDisplayUrl().TrimEnd('/');
                context.SetHeader("Location", displayUrl + "/" + ResourceId);
            }

            if (ContentLocation is not null)
                context.SetHeader("Content-Location", ContentLocation.ToString());

            return Task.CompletedTask;
        }
    }
}
