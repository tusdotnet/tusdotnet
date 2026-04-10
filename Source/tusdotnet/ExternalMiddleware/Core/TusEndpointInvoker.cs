#if NETCOREAPP3_1_OR_GREATER

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.ExternalMiddleware.Core;
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
            var pathBase = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : null;
            var request = DotnetCoreAdapterFactory.CreateRequestAdapter(
                context,
                DotnetCoreRequestUriFactory.GetRequestUri(context)
            );

            var contextAdapter = new ContextAdapter(
                urlPath,
                pathBase,
                EndpointUrlHelper.Instance,
                request,
                config,
                context
            );

            var handled = await TusV1EventRunner.Invoke(contextAdapter);

            if (handled == ResultType.ContinueExecution)
            {
                context.NotFound();
            }
            else
            {
                await TusResponseWriter.WriteToResponse(
                    TusHandleRequestResult.FromResponse(contextAdapter.Response),
                    context
                );
            }
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
