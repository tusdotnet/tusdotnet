using System;

namespace tusdotnet.Adapters
{
    internal class MiddlewareUrlHelper : IUrlHelper
    {
        public static MiddlewareUrlHelper Instance { get; } = new();

        private MiddlewareUrlHelper()
        {
        }

        public string ParseFileId(ContextAdapter context)
        {
            var startIndex = context.Request.RequestUri.LocalPath.IndexOf(context.ConfigUrlPath, StringComparison.OrdinalIgnoreCase) + context.ConfigUrlPath.Length;

#if NETCOREAPP3_1_OR_GREATER

            return context.Request.RequestUri.LocalPath.AsSpan()[startIndex..].Trim('/').ToString();
#else
            return context.Request.RequestUri.LocalPath.Substring(startIndex).Trim('/');
#endif
        }

        public bool UrlMatchesFileIdUrl(ContextAdapter context)
        {
            var requestUri = context.Request.RequestUri;
            var configUrlPath = context.ConfigUrlPath;

            return !UrlMatchesUrlPath(context) && requestUri.LocalPath.StartsWith(configUrlPath, StringComparison.OrdinalIgnoreCase);
        }

        public bool UrlMatchesUrlPath(ContextAdapter context)
        {
            var requestUri = context.Request.RequestUri;
            var configUrlPath = context.ConfigUrlPath;

            return requestUri.LocalPath.TrimEnd('/').Equals(configUrlPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
