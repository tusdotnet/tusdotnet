#if NETCOREAPP3_1_OR_GREATER

namespace tusdotnet.Adapters
{
    internal class EndpointUrlHelper : IUrlHelper
    {
        public static EndpointUrlHelper Instance { get; } = new();

        private EndpointUrlHelper()
        {
        }

        public string ParseFileId(ContextAdapter context)
        {
            return (string)context.HttpContext.Request.RouteValues[EndpointRouteConstants.FileId];
        }

        public bool UrlMatchesFileIdUrl(ContextAdapter context)
        {
            return context.HttpContext.Request.RouteValues.ContainsKey(EndpointRouteConstants.FileId);
        }

        public bool UrlMatchesUrlPath(ContextAdapter context)
        {
            return true;
        }
    }
}

#endif