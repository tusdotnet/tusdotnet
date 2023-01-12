#if NET6_0_OR_GREATER
using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;
using tusdotnet.Runners;

namespace tusdotnet.Runners.TusV1Process
{
    public class WriteFileRequest : TusV1Request
    {
        public string FileId { get; set; }

        public bool InitiatedFromCreationWithUpload { get; set; }

        public long UploadOffset { get; set; }

        public Stream Body { get; set; }

        public PipeReader BodyReader { get; set; }

        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            var adapter = new ContextAdapter("/", EndpointUrlHelper.Instance)
            {
                Request = new()
                {
                    Body = Body,
                    BodyReader = BodyReader,
                    Method = "patch",
                    RequestUri = new Uri("/" + FileId, UriKind.Relative),
                    Headers = RequestHeaders.FromDictionary(new())
                },
                Response = new(),
                Configuration = config,
                FileId = FileId,
                CancellationToken = CancellationToken
            };

            adapter.Request.Headers[HeaderConstants.UploadOffset] = UploadOffset.ToString();

            return adapter;
        }

        internal static WriteFileRequest FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                FileId = context.FileId,
                InitiatedFromCreationWithUpload = false, // TODO: Fix
                Body = context.Request.Body,
                BodyReader = context.Request.BodyReader,
                UploadOffset = context.Request.Headers.UploadOffset,
                CancellationToken = context.CancellationToken
            };
        }
    }
}

#endif