#if NETCOREAPP3_1_OR_GREATER

namespace tusdotnet.Adapters
{
    // This class assumes that it's running for an endpoint and such we already know that
    // the path pattern matches as the code would not run otherwise.
    internal class EndpointUrlHelper : IUrlHelper
    {
        public static EndpointUrlHelper Instance { get; } = new();

        private EndpointUrlHelper() { }

        public string ParseFileId(ContextAdapter context)
        {
            return (string)context.HttpContext.Request.RouteValues[EndpointRouteConstants.FileId];
        }

        public bool UrlMatchesFileIdUrl(ContextAdapter context)
        {
            return context.HttpContext.Request.RouteValues.ContainsKey(
                EndpointRouteConstants.FileId
            );
        }

        public bool UrlMatchesUrlPath(ContextAdapter context)
        {
            return !UrlMatchesFileIdUrl(context);
        }
    }
}

#endif
