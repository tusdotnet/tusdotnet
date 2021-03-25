#if endpointrouting

using System;
using System.IO;
using tusdotnet.Models;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public class WriteContext
    {
        public string FileId { get; internal set; }

        public DateTimeOffset? FileExpires { get; internal set; }

        public long? UploadOffset { get; internal set; }

        public Func<Checksum> GetChecksumProvidedByClient { get; internal set; }

        public bool? ChecksumMatchesTheOneProvidedByClient { get; set; }

        public Stream RequestStream { get; internal set; }

        public bool ClientDisconnectedDuringRead { get; internal set; }

        public bool IsPartialFile { get; set; }

        public bool IsComplete { get; set; }
    }
}

#endif