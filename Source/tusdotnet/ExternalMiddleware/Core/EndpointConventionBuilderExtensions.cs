#if NETCOREAPP3_1_OR_GREATER

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using System;
using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet
{
    /// <summary>
    /// Provides extension methods for Microsoft.AspNetCore.Routing.IEndpointRouteBuilder to add tus endpoints.
    /// </summary>
    public static class EndpointConventionBuilderExtensions
    {
        private const string _tusFileIdRoutePart = $"{{{EndpointRouteConstants.FileId}?}}";
        private const string _tusFileIdRoutePartWithPrefixForwardSlash = $"/{{{EndpointRouteConstants.FileId}?}}";

        /// <summary>
        /// Adds a tus endpoint for the specified pattern. Resolves the configuration to use by looking in the IoC container for the following:
        /// <para>
        /// 1. Func&lt;HttpContext, Task&lt;DefaultTusConfiguration&gt;&gt;
        /// </para>
        /// <para>
        /// 2. DefaultTusConfiguration
        /// </para>
        /// </summary>
        /// <param name="endpoints">The Microsoft.AspNetCore.Routing.IEndpointRouteBuilder to add the tus endpoint to.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <returns>A Microsoft.AspNetCore.Builder.IEndpointConventionBuilder that can be used to further customize the endpoint.</returns>
        public static IEndpointConventionBuilder MapTus(this IEndpointRouteBuilder endpoints, string pattern)
        {
            return endpoints.MapTus(pattern, (Func<HttpContext, Task<DefaultTusConfiguration>>)null);
        }

        /// <summary>
        /// Adds a tus endpoint for the specified pattern using the specified configuration.
        /// </summary>
        /// <param name="endpoints">The Microsoft.AspNetCore.Routing.IEndpointRouteBuilder to add the tus endpoint to.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <param name="configuration">The configuration to use for this specific endpoint.</param>
        /// <returns>A Microsoft.AspNetCore.Builder.IEndpointConventionBuilder that can be used to further customize the endpoint.</returns>
        public static IEndpointConventionBuilder MapTus(this IEndpointRouteBuilder endpoints, string pattern, DefaultTusConfiguration configuration)
        {
            return endpoints.MapTus(pattern, _ => Task.FromResult(configuration));
        }

        /// <summary>
        /// Adds a tus endpoint for the specified pattern using the specified configuration.
        /// </summary>
        /// <param name="endpoints">The Microsoft.AspNetCore.Routing.IEndpointRouteBuilder to add the tus endpoint to.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <param name="configFactory">The configuration factory to use to construct the configuration for this specific endpoint.</param>
        /// <returns>A Microsoft.AspNetCore.Builder.IEndpointConventionBuilder that can be used to further customize the endpoint.</returns>
        public static IEndpointConventionBuilder MapTus(this IEndpointRouteBuilder endpoints, string pattern, Func<HttpContext, Task<DefaultTusConfiguration>> configFactory)
        {
            ThrowIfPatternContainsTusFileId(pattern);

            var routePatternWithFileId = GetRoutePatternWithTusFileId(pattern);
            var invoker = new TusEndpointInvoker(configFactory);

            var name = "tus: " + routePatternWithFileId;

            return endpoints
                .Map(routePatternWithFileId, invoker.Invoke)
                .WithMetadata(new EndpointNameMetadata(name))
                .WithDisplayName(name);
        }

        private static string GetRoutePatternWithTusFileId(string pattern)
        {
            if (pattern.Length == 0)
                return _tusFileIdRoutePartWithPrefixForwardSlash;

            return pattern[^1] == '/'
                ? string.Concat(pattern, _tusFileIdRoutePart)
                : string.Concat(pattern, _tusFileIdRoutePartWithPrefixForwardSlash);
        }

        private static void ThrowIfPatternContainsTusFileId(string pattern)
        {
            var parsedPattern = RoutePatternFactory.Parse(pattern);
            if (parsedPattern.Parameters.Any(x => x.Name.Equals(EndpointRouteConstants.FileId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"Routing pattern must not contain {EndpointRouteConstants.FileId} parameter", nameof(pattern));
            }
        }
    }
}

#endif