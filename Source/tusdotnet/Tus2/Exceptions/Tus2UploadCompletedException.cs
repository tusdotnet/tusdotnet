using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace tusdotnet.Tus2
{
    internal class Tus2UploadCompletedException() : Tus2ProblemDetailsException(HttpStatusCode.BadRequest)
    {
        internal override ProblemDetails GetProblemDetails()
        {
            return new()
            {
                Type = "https://iana.org/assignments/http-problem-types#completed-upload",
                Title = "upload is already completed"
            };
        }
    }
}
