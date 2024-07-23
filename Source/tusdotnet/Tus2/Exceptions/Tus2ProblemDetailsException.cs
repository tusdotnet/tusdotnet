using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace tusdotnet.Tus2
{
    internal abstract class Tus2ProblemDetailsException(HttpStatusCode status, string errorMessage = null) : Tus2AssertRequestException(status, errorMessage)
    {
        internal abstract ProblemDetails GetProblemDetails();
    }
}
