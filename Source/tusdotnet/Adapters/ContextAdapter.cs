using System;
using System.Threading;
using tusdotnet.Models;

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

        public object HttpContext { get; set; }
    }
}