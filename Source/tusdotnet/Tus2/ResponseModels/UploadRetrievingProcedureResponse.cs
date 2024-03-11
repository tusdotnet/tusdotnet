using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class UploadRetrievingProcedureResponse : Tus2BaseResponse
    {
        public bool UploadComplete { get; set; }

        public Uri? ContentLocation { get; set; }

        public UploadRetrievingProcedureResponse()
        {
            NoCache = true;
            Status = HttpStatusCode.NoContent;
        }
        protected override Task WriteResponse(HttpContext context)
        {
            context.SetHeader("Upload-Complete", UploadComplete.ToSfBool());

            if (ContentLocation is not null)
                context.SetHeader("Content-Location", ContentLocation.ToString());

            return Task.CompletedTask;
        }
    }
}