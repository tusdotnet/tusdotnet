using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    // NOTE Copy of the old method based middleware used for benchmarking.

    /// <summary>
    /// Processes tus.io requests for ASP.NET Core.
    /// </summary>
    public class TusCoreMiddlewareOld
    {
        private readonly RequestDelegate _next;

        private readonly Func<HttpContext, Task<DefaultTusConfiguration>> _configFactory;

        /// <summary>Creates a new instance of TusCoreMiddleware.</summary>
        /// <param name="next"></param>
        /// <param name="configFactory"></param>
        public TusCoreMiddlewareOld(RequestDelegate next, Func<HttpContext, Task<DefaultTusConfiguration>> configFactory)
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

            var request = new RequestAdapter(config.UrlPath)
            {
                Headers =
                    context.Request.Headers.ToDictionary(
                        f => f.Key,
                        f => f.Value.ToList(),
                        StringComparer.OrdinalIgnoreCase),
                Body = context.Request.Body,
                Method = context.Request.Method,
                RequestUri = GetRequestUri(context)
            };

            var response = new ResponseAdapter
            {
                Body = context.Response.Body,
                SetHeader = (key, value) => context.Response.Headers[key] = value,
                SetStatus = status => context.Response.StatusCode = (int)status
            };

            var handled = await TusProtocolHandler.Invoke(new ContextAdapter
            {
                Request = request,
                Response = response,
                Configuration = config,
                CancellationToken = context.RequestAborted
            });

            if (!handled)
            {
                await _next(context);
            }
        }

        private static Uri GetRequestUri(HttpContext context)
        {
            return new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}");
        }
    }
}