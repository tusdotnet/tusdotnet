using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class UploadCancellationProcedureResponse : Tus2BaseResponse
    {
        protected override Task WriteResponse(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }
    }
}