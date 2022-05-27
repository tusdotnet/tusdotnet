using System;
using System.Threading;
using Microsoft.AspNetCore.Http;
using tusdotnet.Models;
#if netfull
using Microsoft.Owin;
#endif
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

        public HttpContext HttpContext { get; set; }

#if netfull

        public IOwinContext OwinContext { get; set; }

#endif

        private StoreAdapter _storeAdapter;
        public StoreAdapter StoreAdapter
        {
            get
            {
                return _storeAdapter ??= new StoreAdapter(Configuration.Store, Configuration.AllowedExtensions);
            }
            set
            {
                // Used from creation-with-upload to not have to allocate a new object.
                _storeAdapter = value;
            }
        }

        private readonly Lazy<string> _fileId;
        public string FileId => _fileId.Value;

        /// <summary>
        /// Value specified in DefaultTusConfiguration.UrlPath or the one determined by endpoint routing to be the equivalent of the UrlPath property.
        /// </summary>
        public string ConfigUrlPath => _configUrlPath;
        private readonly string _configUrlPath;
        

        public ContextAdapter(string configUrlPath)
        {
            _configUrlPath = configUrlPath;
            _fileId = new Lazy<string>(() => ParseFileId());
        }

        private string ParseFileId()
        {
            var startIndex = Request.RequestUri.LocalPath.IndexOf(_configUrlPath, StringComparison.OrdinalIgnoreCase) + _configUrlPath.Length;

            // TODO: Add support for endpoint routing

#if NETCOREAPP3_1_OR_GREATER

            return Request.RequestUri.LocalPath.AsSpan()[startIndex..].Trim('/').ToString();
#else

            return Request.RequestUri.LocalPath.Substring(startIndex).Trim('/');
#endif
        }

        public string CreateFileLocation(string fileId)
        {
            return $"{_configUrlPath.TrimEnd('/')}/{fileId}";
        }
    }
}