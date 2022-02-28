using System.IO.Pipelines;
using System.Threading;

namespace tusdotnet.Tus2
{
    public class WriteDataContext
    {
        public CancellationToken CancellationToken { get; set; }

        public PipeReader BodyReader { get; set; }
    }
}
