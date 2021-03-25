#if endpointrouting

using System;
using System.Collections.Generic;
using tusdotnet.Models;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public class CreateContext
    {
        public string FileId { get; internal set; }

        public string UploadMetadata { get; internal set; }

        public IDictionary<string, Metadata> Metadata { get; set; }

        public DateTimeOffset? FileExpires { get; internal set; }

        public long? UploadOffset { get; internal set; }

        public long UploadLength { get; internal set; }
    }
}

#endif