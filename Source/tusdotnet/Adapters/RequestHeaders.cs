using System;
using System.Collections.Generic;
using tusdotnet.Constants;

namespace tusdotnet.Adapters
{
    internal sealed class RequestHeaders
    {
        public string ContentType => this[HeaderConstants.ContentType];

        public string TusResumable => this[HeaderConstants.TusResumable];

        public string UploadChecksum => this[HeaderConstants.UploadChecksum];

        public string UploadConcat => this[HeaderConstants.UploadConcat];

        public string UploadDeferLength => this[HeaderConstants.UploadDeferLength];

        public long UploadLength => _uploadLength.Value;

        public string UploadMetadata => this[HeaderConstants.UploadMetadata];

        public long UploadOffset => long.Parse(this[HeaderConstants.UploadOffset]);

        public string XHttpMethodOveride => this[HeaderConstants.XHttpMethodOveride];

        private readonly Lazy<long> _uploadLength;
        private readonly Dictionary<string, string> _headers;

        public RequestHeaders()
        {
            _uploadLength = new Lazy<long>(() => ParseUploadLength());
        }

        private RequestHeaders(Dictionary<string, string> headers)
            : this()
        {
            _headers = headers;
        }

        public string this[string key]
        {
            get
            {
                return _headers.ContainsKey(key) ? _headers[key] : null;
            }
            set { _headers[key] = value; }
        }

        public bool ContainsKey(string key) => _headers.ContainsKey(key);

        public void Remove(string key) => _headers.Remove(key);

        private long ParseUploadLength()
        {
            return _headers.ContainsKey(HeaderConstants.UploadDeferLength)
                ? -1
                : long.Parse(this[HeaderConstants.UploadLength] ?? "-1");
        }

        public static RequestHeaders FromDictionary(Dictionary<string, string> dictionary)
        {
            var headers = new RequestHeaders(dictionary);
            return headers;
        }

    }
}