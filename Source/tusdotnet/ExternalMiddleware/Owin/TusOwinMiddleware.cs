#if netfull

using Microsoft.Owin;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    /// <summary>
    /// Processes tus.io requests for OWIN.
    /// </summary>
    public class TusOwinMiddleware : OwinMiddleware
    {
        private readonly Func<IOwinRequest, Task<DefaultTusConfiguration>> _configFactory;

        /// <summary>Creates a new instance of TusOwinMiddleware.</summary>
        /// <param name="next"></param>
        /// <param name="configFactory"></param>
        public TusOwinMiddleware(OwinMiddleware next, Func<IOwinRequest, Task<DefaultTusConfiguration>> configFactory) : base(next)
        {
            _configFactory = configFactory;
        }

        /// <summary>
        /// Handles the tus.io request.
        /// </summary>
        /// <param name="context">The IOwinContext</param>
        /// <returns></returns>
        public override async Task Invoke(IOwinContext context)
        {
            var config = await _configFactory(context.Request);

            if (config == null)
            {
                await Next.Invoke(context);
                return;
            }

            MiddlewareConfigurationValidator.Instance.Validate(config);

            if (!RequestIsForTusEndpoint(context.Request.Uri, config.UrlPath))
            {
                await Next.Invoke(context);
                return;
            }

            var request = new RequestAdapter()
            {
                Headers = RequestHeaders.FromDictionary(context.Request.Headers.ToDictionary(f => f.Key, f => f.Value.FirstOrDefault(), StringComparer.OrdinalIgnoreCase)),
                Body = context.Request.Body,
                Method = context.Request.Method,
                RequestUri = context.Request.Uri
            };

            var contextAdapter = new ContextAdapter(config.UrlPath, MiddlewareUrlHelper.Instance)
            {
                Request = request,
                Configuration = config,
                CancellationToken = context.Request.CallCancelled,
                OwinContext = context
            };

            var handled = await TusV1EventRunner.Invoke(contextAdapter);

            if (handled == ResultType.ContinueExecution)
            {
                await Next.Invoke(context);
            }
            else
            {
                await RespondToClient(contextAdapter.Response, context);
            }
        }

        private bool RequestIsForTusEndpoint(Uri requestUri, string urlPath)
        {
            return requestUri.LocalPath.StartsWith(urlPath, StringComparison.OrdinalIgnoreCase);
        }

        private async Task RespondToClient(ResponseAdapter response, IOwinContext context)
        {
            context.Response.StatusCode = (int)response.Status;
            foreach (var item in response.Headers)
            {
                context.Response.Headers[item.Key] = item.Value;
            }

            response.Body.Seek(0, SeekOrigin.Begin);
            await response.Body.CopyToAsync(context.Response.Body);
        }
    }
}

#endif