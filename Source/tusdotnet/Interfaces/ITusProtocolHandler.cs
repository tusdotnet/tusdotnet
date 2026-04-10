using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Handles tus protocol requests. Can be used from middleware, endpoint routing, or directly
    /// from a controller action.
    /// Register via <c>services.AddTus()</c> and inject into your controller or other components.
    /// </summary>
    public interface ITusProtocolHandler
    {
        /// <summary>
        /// Processes the incoming tus request. Does not write anything to <see cref="HttpResponse"/> —
        /// the caller is responsible for applying <see cref="TusHandleRequestResult"/> to the response.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="endpointUrl">
        /// The URL of the tus endpoint. Can be a relative path (e.g. <c>new Uri("/files", UriKind.Relative)</c>)
        /// or an absolute URL (e.g. <c>new Uri("https://example.com/files")</c>).
        /// Used to construct the <c>Location</c> header when creating new uploads and to parse file IDs from the URL.
        /// </param>
        /// <returns>
        /// A <see cref="TusHandleRequestResult"/> with the status code, headers and optional message
        /// to return to the client. If <see cref="TusHandleRequestResult.IsHandled"/> is <c>false</c>
        /// the request did not match the tus endpoint and the caller should continue processing.
        /// </returns>
        Task<TusHandleRequestResult> HandleAsync(
            HttpContext context,
            Uri endpointUrl
        );
    }
}
