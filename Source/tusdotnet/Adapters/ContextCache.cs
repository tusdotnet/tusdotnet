using System;
using System.Collections.Generic;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Adapters
{
    [Obsolete("Remove this entire class and replace it with parsed request headers. UploadOffset can be re-read from storage or fetched from the response")]
    internal class ContextCache
    {
        public Dictionary<string, Metadata> Metadata { get; set; }

        public UploadConcat UploadConcat { get; set; }

        public long? UploadOffset { get; set; }
    }
}
