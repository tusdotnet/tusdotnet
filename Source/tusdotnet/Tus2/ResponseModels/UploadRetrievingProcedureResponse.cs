using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace tusdotnet.Tus2
{
    public class UploadRetrievingProcedureResponse : Tus2BaseResponse
    {
        public bool UploadComplete { get; set; }

        public Uri? ContentLocation { get; set; }

        public TusHandlerLimits? UploadLimit { get; set; }

        public long? UploadLength { get; set; }

        public long UploadOffset { get; set; }

        public UploadRetrievingProcedureResponse()
        {
            NoCache = true;
            Status = HttpStatusCode.NoContent;
        }

        protected override Task WriteResponse(HttpContext context)
        {
            context.SetHeader("Upload-Complete", UploadComplete.ToSfBool());
            context.SetHeader("Upload-Offset", UploadOffset.ToString());

            if (ContentLocation is not null)
                context.SetHeader("Content-Location", ContentLocation.ToString());

            if (UploadLimit is not null)
                context.SetHeader("Upload-Limit", UploadLimit.ToSfDictionary());

            if (UploadLength is not null)
                context.SetHeader("Upload-Length", UploadLength.ToString());

            return Task.CompletedTask;
        }
    }
}
