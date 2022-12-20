#if NETCOREAPP3_1_OR_GREATER

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Models;

namespace tusdotnet
{
    internal class TusEndpointInvoker
    {
        private readonly Func<HttpContext, Task<DefaultTusConfiguration>> _endpointSpecificFactory;

        internal TusEndpointInvoker(Func<HttpContext, Task<DefaultTusConfiguration>> factory)
        {
            _endpointSpecificFactory = factory;
        }

        internal async Task Invoke(HttpContext context)
        {
            var config = await _endpointSpecificFactory.Invoke(context);
            if (config == null)
            {
                context.NotFound();
                return;
            }

            EndpointConfigurationValidator.Instance.Validate(config);

            var urlPath = GetUrlPath(context);
            var request = CreateRequestAdapter(context);

            var contextAdapter = new ContextAdapter(urlPath, EndpointUrlHelper.Instance)
            {
                Request = request,
                Configuration = config,
                CancellationToken = context.RequestAborted,
                HttpContext = context
            };

            var handled = await TusV1EventRunner.Invoke(contextAdapter);

            if (handled == ResultType.ContinueExecution)
            {
                context.NotFound();
            }
            else
            {
                await RespondToClient(contextAdapter.Response, context);
            }
        }

        private static async Task RespondToClient(ResponseAdapter response, HttpContext context)
        {
            // TODO: Implement support for custom responses by not writing if response has started

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

        private static RequestAdapter CreateRequestAdapter(HttpContext context)
        {
            return DotnetCoreAdapterFactory.CreateRequestAdapter(context, DotnetCoreAdapterFactory.GetRequestUri(context));
        }

        private static string GetUrlPath(HttpContext httpContext)
        {
            var fileId = httpContext.GetRouteValue(EndpointRouteConstants.FileId) as string;

            if (string.IsNullOrEmpty(fileId))
                return httpContext.Request.Path;

            var path = httpContext.Request.Path.Value.AsSpan();
            return path[..(path.LastIndexOf('/') + 1)].ToString();

        }
    }
}

#endif