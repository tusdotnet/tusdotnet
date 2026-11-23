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
    /// <summary>
    /// Default implementation of <see cref="Interfaces.ITusProtocolHandler"/>.
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
        public async Task<TusHandleRequestResult> HandleAsync(
            HttpContext httpContext,
            TusEndpointConfiguration endpointConfiguration,
            CancellationToken cancellationToken = default
        )
        {
            AssertContext();

            if (string.IsNullOrWhiteSpace(endpointConfiguration.UrlPath))
            {
                throw new TusConfigurationException(
                    $"{nameof(endpointConfiguration.UrlPath)} cannot be empty."
                );
            }

            // Link the caller supplied cancellation token with the request's abort token so
            // that either source can cancel the operation. This must happen before the
            // ContextAdapter (and its client disconnect guard) is created so that the guard
            // monitors the linked token.
            using var linkedCancellationTokenSource = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(
                    httpContext.RequestAborted,
                    cancellationToken
                )
                : null;

            if (linkedCancellationTokenSource is not null)
            {
                httpContext.RequestAborted = linkedCancellationTokenSource.Token;
            }

            var urlPath = endpointConfiguration.UrlPath;
            var requestPathBase = httpContext.Request.PathBase.Value;
            var requestUri = GetRequestUri(httpContext);

            var request = new RequestAdapter
            {
                Headers = RequestHeaders.FromDictionary(
                    httpContext.Request.Headers.ToDictionary(
                        h => h.Key,
                        h => h.Value.ToString(),
                        StringComparer.OrdinalIgnoreCase
                    )
                ),
                Body = httpContext.Request.Body,
#if pipelines
                BodyReader = httpContext.Request.BodyReader,
#endif
                Method = httpContext.Request.Method,
                RequestUri = requestUri,
            };

            ITrailingHeaderHelper trailingHeaderHelper = null;

#if trailingheaders
            trailingHeaderHelper = new HttpContextTrailingHeaderHelper(httpContext);
#endif

            var contextAdapter = new ContextAdapter(
                urlPath,
                requestPathBase: requestPathBase,
                new ProtocolHandlerUrlHelper(httpContext),
                trailingHeaderHelper,
                request,
                _configuration,
                httpContext
            );

            var result = await TusV1EventRunner.Invoke(contextAdapter);

            if (result == ResultType.ContinueExecution)
                return TusHandleRequestResult.NotHandled;

            return TusHandleRequestResult.FromResponse(contextAdapter.Response);
        }

        private Uri GetRequestUri(HttpContext httpContext)
        {
            return new Uri(
                $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}{httpContext.Request.Path}"
            );
        }

        private void AssertContext()
        {
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
