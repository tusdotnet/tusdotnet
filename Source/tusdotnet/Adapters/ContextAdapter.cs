using System;
using System.Threading;
using tusdotnet.Models;

namespace tusdotnet.Adapters
{
    /// <summary>
    /// Context adapter that handles different pipeline contexts.
    /// </summary>
    internal sealed class ContextAdapter
    {
        public RequestAdapter Request { get; set; }

        public ResponseAdapter Response { get; set; }

        public DefaultTusConfiguration Configuration { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public object HttpContext { get; set; }

        public string FileId => _fileId.Value;

        public ContextAdapter()
        {
            _fileId = new Lazy<string>(ParseFileId);
        }

        private string ParseFileId()
        {
            var startIndex = Request.RequestUri.LocalPath.IndexOf(Configuration.UrlPath, StringComparison.OrdinalIgnoreCase) + Configuration.UrlPath.Length;

            return Request.RequestUri.LocalPath.Substring(startIndex).Trim('/');
        }

        private readonly Lazy<string> _fileId;
    }
}