#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public class CreateFileRequest : TusV1Request
    {
        public Dictionary<string, Metadata>? Metadata { get; set; }

        public string? MetadataString { get; set; }

        public long? UploadLength { get; set; }

        public bool UploadDeferLength { get; set; }

        public string UrlPath { get; set; }


        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            var adapter = new ContextAdapter(UrlPath, EndpointUrlHelper.Instance)
            {
                Request = new()
                {
                    Body = null,
                    BodyReader = null,
                    Method = "post",
                    RequestUri = new Uri(UrlPath, UriKind.Relative),
                },
                Response = new(),
                Configuration = config,
                CancellationToken = CancellationToken
            };

            var headers = new Dictionary<string, string>();
            if (UploadDeferLength)
            {
                headers.Add(HeaderConstants.UploadDeferLength, "1");
            }
            if (MetadataString is not null)
            {
                headers.Add(HeaderConstants.UploadMetadata, MetadataString);
            }
            if (UploadLength is not null)
            {
                headers.Add(HeaderConstants.UploadLength, UploadLength.Value.ToString());
            }

            adapter.Request.Headers = RequestHeaders.FromDictionary(headers);

            return adapter;
        }

        internal static CreateFileRequest FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                Metadata = context.Cache.Metadata,
                MetadataString = context.Request.Headers.UploadMetadata,
                UploadLength = context.Request.Headers.UploadLength,
                UploadDeferLength = context.Request.Headers.UploadDeferLength == "1",
                UrlPath = context.ConfigUrlPath,
                CancellationToken = context.CancellationToken
            };
        }
    }
}

#endif