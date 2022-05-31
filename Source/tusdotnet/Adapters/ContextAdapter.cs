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

        /// <summary>
        /// Get the current file id from the URL. If the setter is used it will override what is currently in the URL. This scenario is used from creation-with-upload when data should be written to a newly created file.
        /// </summary>
        public string FileId
        {
            get
            {
                return _fileId ??= UrlHelper.ParseFileId(this);
            }
            set
            {
                _fileId = value;
            }
        }
        private string _fileId;

        /// <summary>
        /// Value specified in DefaultTusConfiguration.UrlPath or the one determined by endpoint routing to be the equivalent of the UrlPath property.
        /// </summary>
        public string ConfigUrlPath => _configUrlPath;
        private readonly string _configUrlPath;

        public IUrlHelper UrlHelper { get; }

        public ContextAdapter(string configUrlPath, IUrlHelper urlHelper)
        {
            _configUrlPath = configUrlPath;
            UrlHelper = urlHelper;
        }

        public string CreateFileLocation(string fileId)
        {
            return $"{_configUrlPath.TrimEnd('/')}/{fileId}";
        }
    }
}