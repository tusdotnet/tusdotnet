using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using tusdotnet.Adapters;

namespace tusdotnet
{
    internal static class DotnetCoreAdapterFactory
    {
        internal static RequestAdapter CreateRequestAdapter(HttpContext context, string urlPath, Uri requestUri)
        {
            return new RequestAdapter(urlPath)
            {
                Headers =
                    context.Request.Headers.ToDictionary(
                        f => f.Key,
                        f => f.Value.ToList(),
                        StringComparer.OrdinalIgnoreCase),
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

        internal static ResponseAdapter CreateResponseAdapter(HttpContext context)
        {
            return new ResponseAdapter
            {
                Body = context.Response.Body,
                SetHeader = (key, value) => context.Response.Headers[key] = value,
                SetStatus = status => context.Response.StatusCode = (int)status
            };
        }
    }
}
