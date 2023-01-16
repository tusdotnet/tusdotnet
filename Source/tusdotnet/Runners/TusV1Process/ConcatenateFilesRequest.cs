#if NET6_0_OR_GREATER

using System.Collections.Generic;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Runners.TusV1Process
{
    public class ConcatenateFilesRequest : TusV1Request
    {
        public string[] PartialFileIds { get; set; }

        public Dictionary<string, Metadata>? Metadata { get; set; }

        public string? MetadataString { get; set; }

        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            var final = new FileConcatFinal(PartialFileIds);
            final.AddUrlPathToFiles("/");

            var headers = new Dictionary<string, string>
            {
                { HeaderConstants.UploadConcat, final.GetHeader() }
            };

            if (MetadataString is not null)
                headers.Add(HeaderConstants.UploadMetadata, MetadataString);

            return ToContextAdapter("post", config, headers);
        }

        internal static ConcatenateFilesRequest FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                PartialFileIds = ((FileConcatFinal)context.Cache.UploadConcat.Type).Files,
                Metadata = context.Cache.Metadata,
                MetadataString = context.Request.Headers.UploadMetadata,
                CancellationToken = context.CancellationToken
            };
        }
    }
}

#endif