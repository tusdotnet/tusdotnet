using System;

namespace tusdotnet
{
    /// <summary>
    /// Configuration for a specific tus endpoint.
    /// </summary>
    public sealed class TusEndpointConfiguration
    {
        /// <summary>
        /// Creates a new <see cref="TusEndpointConfiguration"/> with the specified base path.
        /// </summary>
        /// <param name="urlPath">
        /// The base path of the tus endpoint, e.g. <c>/files/</c> or <c>/uploads</c>.
        /// This is the base address for all tus resources, not the full request path
        /// of a specific file.
        /// </param>
        public TusEndpointConfiguration(string urlPath)
        {
            UrlPath = urlPath ?? throw new ArgumentNullException(nameof(urlPath));
        }

        /// <summary>
        /// The base path of the tus endpoint, e.g. <c>/files/</c> or <c>/uploads</c>.
        /// </summary>
        public string UrlPath { get; }
    }
}
