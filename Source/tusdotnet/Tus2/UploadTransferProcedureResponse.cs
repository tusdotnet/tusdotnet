using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class UploadTransferProcedureResponse : Tus2BaseResponse
    {
        public bool UploadIncomplete { get; set; }

        protected override Task WriteResponse(HttpContext context)
        {
            if (UploadIncomplete)
            {
                context.SetHeader("Upload-Incomplete", "true");
            }

            return Task.CompletedTask;
        }
    }
}
