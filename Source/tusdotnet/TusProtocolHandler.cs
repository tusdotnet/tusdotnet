using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
using tusdotnet.Models;
#if pipelines
using System.IO.Pipelines;
#endif

namespace tusdotnet
{
    public class TusContext
    {
        public Uri BaseUri { get; set; }

        public string FileId { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public string HttpMethod { get; set; }

        public Stream Body { get; set; }

        public CancellationToken CancellationToken { get; set; }

#if pipelines
        public PipeReader BodyReader { get; set; }
#endif

#if trailingheaders

        public bool SupportsTrailingHeaders { get; set; }

        public Func<bool> CheckTrailingHeadersAvailable { get; set; }

        public Func<string, string> GetTrailingHeader { get; set; }
#endif

        public static TusContext From(HttpContext httpContext)
        {
            return new()
            {
                Body = httpContext.Request.Body,
#if pipelines
                BodyReader = httpContext.Request.BodyReader,
#endif
                CancellationToken = httpContext.RequestAborted,

#if trailingheaders
                CheckTrailingHeadersAvailable = httpContext.Request.CheckTrailersAvailable,
                GetTrailingHeader = (header) =>
                    httpContext.Request.GetTrailer(header).FirstOrDefault(),
                SupportsTrailingHeaders = httpContext.Request.SupportsTrailers(),
#endif
                Headers = GetHeaders(httpContext),
                HttpMethod = httpContext.Request.Method,
                BaseUri = GetBaseUri(httpContext),
                FileId = GetFileId(httpContext),
            };
        }

        private static Uri GetBaseUri(HttpContext httpContext)
        {
            // TODO: Must probably be changed so that it doesn't include the file id in the base uri.
            return new Uri(
                $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}{httpContext.Request.Path}"
            );
        }

        private static Dictionary<string, string> GetHeaders(HttpContext httpContext)
        {
            return httpContext.Request.Headers.ToDictionary(
                h => h.Key,
                h => h.Value.ToString(),
                StringComparer.OrdinalIgnoreCase
            );
        }

        private static string GetFileId(HttpContext httpContext)
        {
            // TODO: Make it better
            return httpContext.Request.Path.Value.Split('/').LastOrDefault();
        }
    }

    /// <summary>
    /// Default implementation of <see cref="ITusProtocolHandler"/>.
    /// </summary>
    internal sealed class TusProtocolHandler : ITusProtocolHandler
    {
        private readonly DefaultTusConfiguration _configuration;

        internal TusProtocolHandler(DefaultTusConfiguration configuration)
        {
            _configuration =
                configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <inheritdoc />
        public async Task<TusHandleRequestResult> HandleAsync(TusContext context)
        {
            AssertContext(context);

            var endpointUrlString = context.BaseUri.ToString();
            var requestUri = context.BaseUri;

            var request = new RequestAdapter
            {
                Headers = RequestHeaders.FromDictionary(context.Headers),
                Body = context.Body,
#if pipelines
                BodyReader = context.BodyReader,
#endif
                Method = context.HttpMethod,
                RequestUri = requestUri,
            };

            ITrailingHeaderHelper trailingHeaderHelper = null;

#if trailingheaders
            trailingHeaderHelper = new TusContextTrailingHeaderHelper(context);
#endif

            var contextAdapter = new ContextAdapter(
                endpointUrlString,
                requestPathBase: null,
                new ProtocolHandlerUrlHelper(context),
                trailingHeaderHelper,
                request,
                _configuration,
                context.CancellationToken
            );

            var result = await TusV1EventRunner.Invoke(contextAdapter);

            if (result == ResultType.ContinueExecution)
                return TusHandleRequestResult.NotHandled;

            return TusHandleRequestResult.FromResponse(contextAdapter.Response);
        }

        private void AssertContext(TusContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_configuration.Store == null)
            {
                throw new TusConfigurationException(
                    $"{nameof(_configuration.Store)} cannot be null."
                );
            }

            if (
                !Enum.IsDefined(
                    typeof(MetadataParsingStrategy),
                    _configuration.MetadataParsingStrategy
                )
            )
            {
                throw new TusConfigurationException(
                    $"{nameof(MetadataParsingStrategy)} is not a valid value."
                );
            }
        }
    }
}
