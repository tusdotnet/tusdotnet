using System;
using System.Net;

namespace tusdotnet.Tus2
{
    internal class Tus2AssertRequestException : Exception
    {
        public Tus2AssertRequestException(HttpStatusCode status, string errorMessage = null)
            : base($"Request exception: {status} - {errorMessage}")
        {
            Status = status;
            ErrorMessage = errorMessage;
        }

        public HttpStatusCode Status { get; set; }

        public string ErrorMessage { get; set; }
    }
}
