using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    public class WriteDataContext
    {
        public CancellationToken CancellationToken { get; set; }

        public PipeReader BodyReader { get; set; }

        public IDictionary<string, Metadata> Metadata { get; set; }
    }
}
