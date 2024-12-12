using System;
using Microsoft.AspNetCore.Http;

namespace tusdotnet.ExternalMiddleware.Core
{
    internal static class DotnetCoreRequestUriFactory
    {
        internal static Uri GetRequestUri(HttpContext context)
        {
            return new Uri(
                $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}"
            );
        }
    }
}
