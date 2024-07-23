using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace tusdotnet.Tus2
{
    public static class EndpointRouteBuilderExtensions
    {
        private static class EndpointRouteConstants
        {
            internal const string ResourceId = "ResumableUploadResourceId";
        }

        private const string _tusFileIdRoutePart = $"{{{EndpointRouteConstants.ResourceId}?}}";
        private const string _tusFileIdRoutePartWithPrefixForwardSlash = $"/{{{EndpointRouteConstants.ResourceId}?}}";

        public static IEndpointConventionBuilder MapTus2<T>(this IEndpointRouteBuilder endpoints, string route) where T : TusHandler
        {
            var routePatternWithFileId = GetRoutePatternWithTusFileId(route);

            return endpoints.Map(routePatternWithFileId, async httpContext =>
            {
                var resourceId = httpContext.GetRouteValue(EndpointRouteConstants.ResourceId) as string;

                await Tus2Endpoint.Invoke<T>(httpContext, resourceId);
            });
        }

        private static string GetRoutePatternWithTusFileId(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Length == 0)
                return _tusFileIdRoutePartWithPrefixForwardSlash;

            return pattern[^1] == '/'
                ? string.Concat(pattern, _tusFileIdRoutePart)
                : string.Concat(pattern, _tusFileIdRoutePartWithPrefixForwardSlash);
        }
    }
}
