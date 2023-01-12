#if NET6_0_OR_GREATER
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public class WriteFileRequest : TusV1Request
    {
        public string FileId { get; set; }

        public bool InitiatedFromCreationWithUpload { get; set; }

        public long UploadOffset { get; set; }


        public string Checksum { get; set; }

        public Stream Body { get; set; }

        public PipeReader BodyReader { get; set; }

        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            var headers = new Dictionary<string, string>
            {
                { HeaderConstants.UploadOffset, UploadOffset.ToString() },
                { HeaderConstants.UploadChecksum, Checksum },
            };

            var adapter = ToContextAdapter("patch", config, headers, fileId: FileId);
            adapter.Request.Body = Body;
            adapter.Request.BodyReader = BodyReader;

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
                CancellationToken = context.CancellationToken,
                Checksum = context.Request.Headers.UploadChecksum
            };
        }
    }
}

#endif