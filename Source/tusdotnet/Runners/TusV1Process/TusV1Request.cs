#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using tusdotnet.Adapters;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public abstract class TusV1Request
    {
        private static readonly Dictionary<string, string> _emptyHeaders = new();

        public CancellationToken CancellationToken { get; set; }

        private protected ContextAdapter ToContextAdapter(string method, DefaultTusConfiguration config, Dictionary<string, string> headers = null, string fileId = null, string urlPath = null)
        {
            var requestUrl = urlPath ?? "/";
            if (fileId is not null)
            {
                requestUrl += fileId;
            }

            // TODO: Replace EndpointUrlHelper.Instance with something else?
            var adapter = new ContextAdapter(urlPath ?? "/", EndpointUrlHelper.Instance)
            {
                Request = new()
                {
                    Body = null,
                    BodyReader = null,
                    Method = method,
                    RequestUri = new Uri(requestUrl, UriKind.Relative),
                    Headers = RequestHeaders.FromDictionary(headers ?? _emptyHeaders)
                },
                Response = new(),
                Configuration = config,
                CancellationToken = CancellationToken,
                FileId = fileId
            };

            return adapter;
        }
    }
}

#endif