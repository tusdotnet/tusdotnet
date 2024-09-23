using System.Collections.Generic;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Adapters
{
    internal class ContextCache
    {
        public Dictionary<string, Metadata> Metadata { get; set; }

        public UploadConcat UploadConcat { get; set; }

        public Checksum UploadChecksum { get; set; }

        public long? UploadOffset { get; set; }
        
    }
}
