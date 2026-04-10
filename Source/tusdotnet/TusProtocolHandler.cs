using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet
{
    /// <summary>
    /// Default implementation of <see cref="ITusProtocolHandler"/>.
    /// </summary>
    internal sealed class TusProtocolHandler : ITusProtocolHandler
    {
        private readonly DefaultTusConfiguration _configuration;

        internal TusProtocolHandler(DefaultTusConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <inheritdoc />
        public async Task<TusHandleRequestResult> HandleAsync(
            HttpContext context,
            Uri endpointUrl
        )
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (endpointUrl == null)
                throw new ArgumentNullException(nameof(endpointUrl));

            if (_configuration.Store == null)
                throw new TusConfigurationException($"{nameof(_configuration.Store)} cannot be null.");

            if (!Enum.IsDefined(typeof(MetadataParsingStrategy), _configuration.MetadataParsingStrategy))
                throw new TusConfigurationException($"{nameof(MetadataParsingStrategy)} is not a valid value.");

            var endpointUrlString = endpointUrl.IsAbsoluteUri
                ? endpointUrl.AbsoluteUri
                : endpointUrl.OriginalString;

            var requestUri = new Uri(
                $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}"
            );

            var request = new RequestAdapter
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

            var contextAdapter = new ContextAdapter(
                endpointUrlString,
                requestPathBase: null,
                MiddlewareUrlHelper.Instance,
                request,
                _configuration,
                context
            );

            var result = await TusV1EventRunner.Invoke(contextAdapter);

            if (result == ResultType.ContinueExecution)
                return TusHandleRequestResult.NotHandled;

            return TusHandleRequestResult.FromResponse(contextAdapter.Response);
        }
    }
}
