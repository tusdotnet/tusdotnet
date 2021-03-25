using System.Threading;
using Microsoft.AspNetCore.Http;
using tusdotnet.ExternalMiddleware.Core;
#if endpointrouting
using tusdotnet.ExternalMiddleware.EndpointRouting;
#endif
using tusdotnet.Models;
#if netfull
using Microsoft.Owin;
#endif
namespace tusdotnet.Adapters
{
    /// <summary>
    /// Context adapter that handles different pipeline contexts.
    /// </summary>
    internal sealed class ContextAdapter
    {
        public RequestAdapter Request { get; set; }

        public ResponseAdapter Response { get; set; }

        public DefaultTusConfiguration Configuration { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public HttpContext HttpContext { get; set; }

#if netfull

        public IOwinContext OwinContext { get; set; }

#endif

#if endpointrouting
        public EndpointOptions EndpointOptions { get; set; }
#endif
    }
}