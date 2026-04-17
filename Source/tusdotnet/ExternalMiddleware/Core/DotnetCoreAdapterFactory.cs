using System;
using System.Collections.Generic;
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
                Headers = RequestHeaders.FromDictionary(BuildHeaderDictionary(context.Request.Headers)),
                Body = context.Request.Body,
#if pipelines
                BodyReader = context.Request.BodyReader,
#endif
                Method = context.Request.Method,
                RequestUri = requestUri,
            };
        }

        private static Dictionary<string, string> BuildHeaderDictionary(IHeaderDictionary headers)
        {
            var dict = new Dictionary<string, string>(headers.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in headers)
            {
                dict[kvp.Key] = kvp.Value[0];
            }
            return dict;
        }
    }
}
