using tusdotnet.Constants;

namespace tusdotnet.Helpers
{
    /// <summary>
    /// Helper class for setting upp cross origin resource sharing (CORS).
    /// </summary>
    public static class CorsHelper
    {
        /// <summary>
        /// Returns response headers that a browser-based tus client needs to read cross-origin.
        /// </summary>
        /// <returns>Response headers to expose using Access-Control-Expose-Headers</returns>
        public static string[] GetExposedHeaders()
        {
            return new[]
            {
                HeaderConstants.Location,
                HeaderConstants.TusResumable,
                HeaderConstants.TusVersion,
                HeaderConstants.TusExtension,
                HeaderConstants.TusMaxSize,
                HeaderConstants.TusChecksumAlgorithm,
                HeaderConstants.UploadLength,
                HeaderConstants.UploadDeferLength,
                HeaderConstants.UploadOffset,
                HeaderConstants.UploadMetadata,
                HeaderConstants.UploadConcat,
                HeaderConstants.UploadExpires,
            };
        }

        /// <summary>
        /// Returns request headers that a browser-based tus client may send cross-origin.
        /// </summary>
        /// <returns>Request headers to allow using Access-Control-Allow-Headers</returns>
        public static string[] GetAllowedHeaders()
        {
            return new[]
            {
                HeaderConstants.TusResumable,
                HeaderConstants.UploadLength,
                HeaderConstants.UploadDeferLength,
                HeaderConstants.UploadOffset,
                HeaderConstants.UploadMetadata,
                HeaderConstants.UploadChecksum,
                HeaderConstants.UploadConcat,
                HeaderConstants.ContentType,
                HeaderConstants.XHttpMethodOveride,
            };
        }

        /// <summary>
        /// Returns HTTP methods used by the tus protocol.
        /// </summary>
        /// <returns>HTTP methods to allow using Access-Control-Allow-Methods</returns>
        public static string[] GetAllowedMethods()
        {
            return new[] { "OPTIONS", "POST", "HEAD", "PATCH", "DELETE" };
        }
    }
}
