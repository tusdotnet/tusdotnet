#if NET6_0_OR_GREATER
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions.Internal;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public class WriteFileRequest : TusV1Request
    {
        public string FileId { get; set; }

        public long UploadOffset { get; set; }

        public long? UploadLength { get; set; }

        public string Checksum { get; set; }

        public Stream Body { get; set; }

        public PipeReader BodyReader { get; set; }

        internal bool InitiatedFromCreationWithUpload { get; set; }

        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            var headers = new Dictionary<string, string>
            {
                { HeaderConstants.UploadOffset, UploadOffset.ToString() },
            };

            if (UploadLength is not null)
                headers.Add(HeaderConstants.UploadLength, UploadLength.ToString());

            if (Checksum is not null)
                headers.Add(HeaderConstants.UploadChecksum, Checksum);

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
                Body = context.Request.Body,
                BodyReader = context.Request.BodyReader,
                UploadOffset = context.Request.Headers.UploadOffset,
                UploadLength = context.Request.Headers.UploadLength == -1 ? null : context.Request.Headers.UploadLength,
                CancellationToken = context.CancellationToken,
                Checksum = context.Request.Headers.UploadChecksum,

                // Called from POST request to create the file. Set to true in that case to bypass some validations.
                InitiatedFromCreationWithUpload = context.Request.GetHttpMethod() == "post"
            };
        }
    }
}

#endif