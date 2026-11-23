using System;
using Microsoft.AspNetCore.Http;

namespace tusdotnet.Adapters
{
    internal class ProtocolHandlerUrlHelper : IUrlHelper
    {
        private readonly HttpContext _httpContext;

        public ProtocolHandlerUrlHelper(HttpContext httpContext)
        {
            _httpContext = httpContext;
        }

        public string ParseFileId(ContextAdapter context)
        {
#if NETCOREAPP3_1_OR_GREATER
            // When running in ASP.NET Core with endpoint routing, the file id is available as a
            // route value (e.g. "/files/{TusFileId}"). Check multiple common key names since the
            // parameter name depends on how the route is defined:
            //   [HttpPatch("{fileId}")]  -> key is "fileId"
            //   endpoint pattern "{TusFileId?}" -> key is "TusFileId"
            //   [HttpPatch("{id}")]      -> key is "id"
            foreach (var key in new[] { "TusFileId", "fileId", "id" })
            {
                if (_httpContext.Request.RouteValues.TryGetValue(key, out var routeValue) &&
                    routeValue is string fileId &&
                    !string.IsNullOrEmpty(fileId))
                {
                    return fileId;
                }
            }
#endif

            // Fallback: extract the file id from the request path by stripping the configured
            // UrlPath prefix. The prefix is matched on a segment boundary so that e.g. "/files"
            // does not match "/files-other/<id>".
            return GetFileIdFromPath(_httpContext.Request.Path.Value, context.ConfigUrlPath);
        }

        public bool UrlMatchesFileIdUrl(ContextAdapter context)
        {
            var path = _httpContext.Request.Path.Value;

            return !UrlMatchesUrlPath(context)
                && PathStartsWithUrlPath(path, context.ConfigUrlPath);
        }

        public bool UrlMatchesUrlPath(ContextAdapter context)
        {
            var path = _httpContext.Request.Path.Value;

            return NormalizeUrlPath(path)
                .Equals(NormalizeUrlPath(context.ConfigUrlPath), StringComparison.OrdinalIgnoreCase);
        }

        // "/files/" -> "/files", "/" -> "", "" -> ""
        private static string NormalizeUrlPath(string urlPath)
        {
            return urlPath.TrimEnd('/');
        }

        // Matches only on a segment boundary, so "/files" does not match "/files-other/<id>".
        private static bool PathStartsWithUrlPath(string path, string configUrlPath)
        {
            var prefix = NormalizeUrlPath(configUrlPath) + "/";

            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFileIdFromPath(string path, string configUrlPath)
        {
            var prefix = NormalizeUrlPath(configUrlPath) + "/";

            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

#if NETCOREAPP3_1_OR_GREATER
            return path.AsSpan(prefix.Length).Trim('/').ToString();
#else
            return path.Substring(prefix.Length).Trim('/');
#endif
        }
    }
}
