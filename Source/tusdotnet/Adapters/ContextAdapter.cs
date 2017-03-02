using System.Threading;
using tusdotnet.Interfaces;

namespace tusdotnet.Adapters
{
    internal class ContextAdapter
    {
	    public RequestAdapter Request { get; set; }
	    public ResponseAdapter Response { get; set; }
	    public ITusConfiguration Configuration { get; set; }
	    public CancellationToken CancellationToken { get; set; }
    }
}
