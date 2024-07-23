using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace tusdotnet.Tus2
{
    internal class Tus2MismatchingUploadOffsetException(long expectedOffset, long providedOffset) : Tus2ProblemDetailsException(HttpStatusCode.Conflict, errorMessage: null)
    {
        public long ExpectedOffset { get; init; } = expectedOffset;

        public long ProvidedOffset { get; init; } = providedOffset;

        internal override ProblemDetails GetProblemDetails()
        {
            return new()
            {
                Type = "https://iana.org/assignments/http-problem-types#mismatching-upload-offset",
                Title = "offset from request does not match offset of resource",
                Extensions =
                {
                    { "exected-offset", ExpectedOffset },
                    { "provided-offset", ProvidedOffset }
                }
            };
        }
    }
}
