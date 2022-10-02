using System;
using System.Collections.Generic;
using System.IO;
using tusdotnet.Constants;
#if pipelines
using System.IO.Pipelines;
#endif

namespace tusdotnet.Adapters
{
    /// <summary>
    /// Request adapter that handles different pipeline requests.
    /// </summary>
    internal sealed class RequestAdapter
    {
        public string Method { get; set; }

        public Uri RequestUri { get; set; }

        public Stream Body { get; set; }

#if pipelines
        public PipeReader BodyReader { get; set; }
#endif

        public Dictionary<string, List<string>> Headers { get; set; }

        public string ContentType => GetHeader("Content-Type");

        public string FileId => _fileId.Value;

        public string ConfigUrlPath => _configUrlPath;

        public long UploadLength => _uploadLength.Value;

        public long UploadOffset => _uploadOffset.Value;

        private readonly Lazy<string> _fileId;
        private readonly Lazy<long> _uploadLength;
        private readonly Lazy<long> _uploadOffset;
        private readonly string _configUrlPath;

        public RequestAdapter(string configUrlPath)
        {
            _fileId = new Lazy<string>(() => ParseFileId());
            _uploadLength = new Lazy<long>(() => ParseUploadLength());
            _uploadOffset = new Lazy<long>(() => ParseUploadOffset());
            _configUrlPath = configUrlPath;
        }

        public string GetHeader(string name)
        {
            return Headers?.ContainsKey(name) == true ? Headers[name][0] : null;
        }
        private string ParseFileId()
        {
            var startIndex = RequestUri.LocalPath.IndexOf(_configUrlPath, StringComparison.OrdinalIgnoreCase) + _configUrlPath.Length;

#if NETCOREAPP3_1_OR_GREATER

            return RequestUri.LocalPath.AsSpan()[startIndex..].Trim('/').ToString();
#else

            return RequestUri.LocalPath.Substring(startIndex).Trim('/');
#endif
        }

        private long ParseUploadLength()
        {
            return Headers.ContainsKey(HeaderConstants.UploadDeferLength)
                ? -1
                : long.Parse(GetHeader(HeaderConstants.UploadLength) ?? "-1");
        }

        private long ParseUploadOffset()
        {
            return long.Parse(GetHeader(HeaderConstants.UploadOffset));
        }
    }
}