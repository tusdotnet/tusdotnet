using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class UploadRetrievingProcedureResponse : Tus2BaseResponse
    {
        public long UploadOffset { get; set; }

        public UploadRetrievingProcedureResponse()
        {
            NoCache = true;
        }

        protected override Task WriteResponse(HttpContext context)
        {
            context.Response.Headers["Upload-Offset"] = new StringValues(UploadOffset.ToString());
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }
    }
}