using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class CreateFileProcedureResponse : Tus2BaseResponse
    {
        public CreateFileProcedureResponse()
        {
            Status = HttpStatusCode.Created;
        }

        protected override Task WriteResponse(HttpContext context)
        {
            // Never actually sent to the client. Just used internally between methods.
            throw new NotImplementedException();
        }
    }
}
