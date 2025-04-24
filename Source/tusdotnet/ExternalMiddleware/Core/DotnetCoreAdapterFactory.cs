using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;

namespace tusdotnet
{
    internal static class DotnetCoreAdapterFactory
    {
        internal static RequestAdapter CreateRequestAdapter(HttpContext context, Uri requestUri)
        {
            return new RequestAdapter()
            {
                Headers = RequestHeaders.FromDictionary(
                    context.Request.Headers.ToDictionary(
                        f => f.Key,
                        f => f.Value.FirstOrDefault(),
                        StringComparer.OrdinalIgnoreCase
                    )
                ),
                Body = context.Request.Body,
#if pipelines
                BodyReader = context.Request.BodyReader,
#endif
                Method = context.Request.Method,
                RequestUri = requestUri,
            };
        }
    }
}
