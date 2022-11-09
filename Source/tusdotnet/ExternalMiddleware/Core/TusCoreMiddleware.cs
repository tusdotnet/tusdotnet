using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    /// <summary>
    /// Processes tus.io requests for ASP.NET Core.
    /// </summary>
    public class TusCoreMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly Func<HttpContext, Task<DefaultTusConfiguration>> _configFactory;

        /// <summary>Creates a new instance of TusCoreMiddleware.</summary>
        /// <param name="next"></param>
        /// <param name="configFactory"></param>
        public TusCoreMiddleware(RequestDelegate next, Func<HttpContext, Task<DefaultTusConfiguration>> configFactory)
        {
            _next = next;
            _configFactory = configFactory;
        }

        /// <summary>
        /// Handles the tus.io request.
        /// </summary>
        /// <param name="context">The HttpContext</param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            var config = await _configFactory(context);

            if (config == null)
            {
                await _next(context);
                return;
            }

            MiddlewareConfigurationValidator.Instance.Validate(config);

            var requestUri = DotnetCoreAdapterFactory.GetRequestUri(context);

            if (!TusProtocolHandlerIntentBased.RequestIsForTusEndpoint(requestUri, config))
            {
                await _next(context);
                return;
            }

            var request = DotnetCoreAdapterFactory.CreateRequestAdapter(context, requestUri);
            var response = DotnetCoreAdapterFactory.CreateResponseAdapter(context);

            var handled = await TusProtocolHandlerIntentBased.Invoke(new ContextAdapter(config.UrlPath, MiddlewareUrlHelper.Instance)
            {
                Request = request,
                Response = response,
                Configuration = config,
                CancellationToken = context.RequestAborted,
                HttpContext = context
            });

            if (handled == ResultType.ContinueExecution)
            {
                await _next(context);
            }
        }
    }
}