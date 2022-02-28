using System.Collections.Generic;
using System.Threading;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    public class CreateFileContext
    {
        public CancellationToken CancellationToken { get; set; }

        public Dictionary<string, Metadata> Metadata { get; set; }
    }
}
