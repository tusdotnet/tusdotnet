#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Http;

namespace tusdotnet.Extensions
{
    internal static class HttpContextExtensions
    {
        internal static void NotFound(this HttpContext httpContext) => httpContext.Response.StatusCode = 404;
    }
}
#endif