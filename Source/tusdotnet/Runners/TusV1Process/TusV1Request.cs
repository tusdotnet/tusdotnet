#if NET6_0_OR_GREATER
using System.Threading;

namespace tusdotnet.Runners.TusV1Process
{
    public abstract class TusV1Request
    {
        public CancellationToken CancellationToken { get; set; }
    }
}

#endif