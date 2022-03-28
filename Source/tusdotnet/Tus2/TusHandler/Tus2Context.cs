using Microsoft.AspNetCore.Http;
using System.Threading;

namespace tusdotnet.Tus2
{
    public abstract class Tus2Context
    {
        internal HttpContext HttpContext { get; set; }

        public Tus2Headers Headers { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }
}
