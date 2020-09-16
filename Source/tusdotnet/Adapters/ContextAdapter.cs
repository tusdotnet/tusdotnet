using System.Threading;
using Microsoft.AspNetCore.Http;
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

        internal string CreateLocationHeaderValue(string fileId)
        {
            return $"{Configuration.UrlPath.TrimEnd('/')}/{fileId}";
        }

        internal string GetUsername()
        {
            // TODO: Add support for OWIN
            return HttpContext.User.Identity.Name;
        }
    }
}