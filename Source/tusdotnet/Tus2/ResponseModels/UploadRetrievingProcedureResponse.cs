using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class UploadRetrievingProcedureResponse : Tus2BaseResponse
    {
        public long UploadOffset { get; set; }

        public bool UploadIncomplete { get; set; }

        public UploadRetrievingProcedureResponse()
        {
            NoCache = true;
        }

        protected override Task WriteResponse(HttpContext context)
        {
            context.SetHeader("Upload-Offset", UploadOffset.ToString());
            context.SetHeader("Upload-Incomplete", UploadIncomplete.ToSfBool());

            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }
    }
}