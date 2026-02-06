using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.ExternalMiddleware.Core;
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
        public TusCoreMiddleware(
            RequestDelegate next,
            Func<HttpContext, Task<DefaultTusConfiguration>> configFactory
        )
        {
            _next = next;
            _configFactory = configFactory;
        }

        /// <summary>
        /// Handles the tus.io request.
        /// </summary>
        /// <param name="httpContext">The HttpContext</param>
        /// <returns></returns>
        public async Task Invoke(HttpContext httpContext)
        {
            var config = await _configFactory(httpContext);

            if (config == null)
            {
                await _next(httpContext);
                return;
            }

            MiddlewareConfigurationValidator.Instance.Validate(config);

            var requestUri = DotnetCoreRequestUriFactory.GetRequestUri(httpContext);

            if (!RequestIsForTusEndpoint(requestUri, config.UrlPath))
            {
                await _next(httpContext);
                return;
            }

            var request = DotnetCoreAdapterFactory.CreateRequestAdapter(httpContext, requestUri);

            // Note: When using the middleware one must prefix the UrlPath with the base path so no need to provide it here.
            // This is done for backwards compatibility.
            var contextAdapter = new ContextAdapter(
                config.UrlPath,
                requestPathBase: null,
                MiddlewareUrlHelper.Instance,
                request,
                config,
                httpContext
            );

            var handled = await TusV1EventRunner.Invoke(contextAdapter);

            if (handled == ResultType.ContinueExecution)
            {
                await _next(httpContext);
            }
            else
            {
                await RespondToClient(contextAdapter.Response, httpContext);
            }
        }

        private static bool RequestIsForTusEndpoint(Uri requestUri, string urlPath)
        {
            return requestUri.LocalPath.StartsWith(urlPath, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task RespondToClient(ResponseAdapter response, HttpContext context)
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                context.Abort();
                return;
            }

            context.Response.StatusCode = (int)response.Status;
            foreach (var item in response.Headers)
            {
                context.Response.Headers[item.Key] = item.Value;
            }

            if (string.IsNullOrWhiteSpace(response.Message))
                return;

            context.Response.ContentType = "text/plain";
            await response.WriteMessageToStream(context.Response.Body);
        }
    }
}
