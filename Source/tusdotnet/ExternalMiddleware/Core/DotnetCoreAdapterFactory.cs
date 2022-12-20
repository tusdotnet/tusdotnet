using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;

namespace tusdotnet
{
    internal static class DotnetCoreAdapterFactory
    {
        internal static RequestAdapter CreateRequestAdapter(HttpContext context, Uri requestUri)
        {
            return new RequestAdapter()
            {
                Headers = RequestHeaders.FromDictionary(context.Request.Headers.ToDictionary(
                        f => f.Key,
                        f => f.Value.FirstOrDefault(),
                        StringComparer.OrdinalIgnoreCase)),
                Body = context.Request.Body,
#if pipelines
                BodyReader = context.Request.BodyReader,
#endif
                Method = context.Request.Method,
                RequestUri = GetRequestUri(context)
            };
        }

        internal static Uri GetRequestUri(HttpContext context)
        {
            return new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}");
        }
    }
}
