using System;
using System.Threading;
using Microsoft.AspNetCore.Http;
using tusdotnet.Helpers;
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
        public RequestAdapter Request { get; }

        // Note: Needs to be settable as we wish to reuse the context for multi intent handling, e.g. creation-with-upload.
        public ResponseAdapter Response { get; set; }

        public DefaultTusConfiguration Configuration { get; }

        public CancellationToken CancellationToken => ClientDisconnectGuard.GuardedToken;

        public HttpContext HttpContext { get; }

#if netfull

        public IOwinContext OwinContext { get; }
#endif

        public ContextCache Cache { get; }

        public ClientDisconnectGuardWithTimeout ClientDisconnectGuard { get; private set; }

        private StoreAdapter _storeAdapter;
        public StoreAdapter StoreAdapter
        {
            get
            {
                return _storeAdapter ??= new StoreAdapter(
                    Configuration.Store,
                    Configuration.AllowedExtensions
                );
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
            get { return _fileId ??= UrlHelper.ParseFileId(this); }
            set { _fileId = value; }
        }
        private string _fileId;

        /// <summary>
        /// Value specified in DefaultTusConfiguration.UrlPath or the one determined by endpoint routing to be the equivalent of the UrlPath property.
        /// </summary>
        public string ConfigUrlPath => _configUrlPath;
        private readonly string _configUrlPath;
        private readonly string _requestPathBase;

        public IUrlHelper UrlHelper { get; }

        public ContextAdapter(
            string configUrlPath,
            string requestPathBase,
            IUrlHelper urlHelper,
            RequestAdapter request,
            DefaultTusConfiguration config,
            HttpContext httpContext
        )
        {
            _configUrlPath = configUrlPath;
            _requestPathBase = requestPathBase;
            UrlHelper = urlHelper;
            Request = request;
            Configuration = config;
            HttpContext = httpContext;

            Cache = new();

            if (httpContext is not null)
            {
                SetupClientDisconnectGuard(httpContext.RequestAborted, config.ClientReadTimeout);
                httpContext.RequestAborted = ClientDisconnectGuard.GuardedToken;
            }
        }

#if netfull

        public ContextAdapter(
            string configUrlPath,
            IUrlHelper urlHelper,
            RequestAdapter request,
            DefaultTusConfiguration config,
            IOwinContext owinContext
        )
            : this(
                configUrlPath,
                requestPathBase: null,
                urlHelper,
                request,
                config,
                null as HttpContext
            )
        {
            OwinContext = owinContext;

            SetupClientDisconnectGuard(owinContext.Request.CallCancelled, config.ClientReadTimeout);
            owinContext.Request.CallCancelled = ClientDisconnectGuard.GuardedToken;
        }
#endif

        public string CreateFileLocation(string fileId)
        {
            return $"{ResolveEndpointUrlWithoutTrailingSlash()}/{fileId}";
        }

        public string ResolveEndpointUrlWithoutTrailingSlash()
        {
            if (string.IsNullOrWhiteSpace(_requestPathBase))
                return $"{_configUrlPath.TrimEnd('/')}";

            return $"{_requestPathBase.TrimEnd('/')}/{_configUrlPath.Trim('/')}";
        }

        private void SetupClientDisconnectGuard(
            CancellationToken? tokenToMonitor,
            TimeSpan executionTimeout
        )
        {
            if (tokenToMonitor is null)
                return;

            ClientDisconnectGuard = new ClientDisconnectGuardWithTimeout(
                executionTimeout,
                tokenToMonitor.Value
            );
        }
    }
}
