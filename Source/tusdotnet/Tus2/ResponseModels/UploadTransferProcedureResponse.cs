using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class UploadTransferProcedureResponse : Tus2BaseResponse
    {
        public bool UploadIncomplete { get; set; }

        protected override Task WriteResponse(HttpContext context)
        {
            if (UploadIncomplete)
            {
                context.SetHeader("Upload-Incomplete", UploadIncomplete.ToSfBool());
            }

            return Task.CompletedTask;
        }
    }
}
