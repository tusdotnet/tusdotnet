using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class UploadRetrievingProcedureResponse : Tus2BaseResponse
    {
        public bool UploadIncomplete { get; set; }

        public UploadRetrievingProcedureResponse()
        {
            NoCache = true;
            Status = HttpStatusCode.NoContent;
        }
        protected override Task WriteResponse(HttpContext context)
        {
            context.SetHeader("Upload-Incomplete", UploadIncomplete.ToSfBool());

            return Task.CompletedTask;
        }
    }
}