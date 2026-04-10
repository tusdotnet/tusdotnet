using System.Collections.Generic;
using System.Net;

namespace tusdotnet
{
    /// <summary>
    /// Result returned by <see cref="Interfaces.ITusProtocolHandler.HandleAsync"/>.
    /// </summary>
    public sealed class TusHandleRequestResult
    {
        /// <summary>
        /// <c>true</c> if the request was handled by tusdotnet; <c>false</c> if it was not a tus
        /// request and the caller should continue processing (e.g. call next middleware or return 404).
        /// </summary>
        public bool IsHandled { get; }

        /// <summary>
        /// The HTTP status code to return to the client. Only relevant when <see cref="IsHandled"/> is <c>true</c>.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Response headers to return to the client. Only relevant when <see cref="IsHandled"/> is <c>true</c>.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>
        /// Optional response body message. Only relevant when <see cref="IsHandled"/> is <c>true</c>.
        /// Will be <c>null</c> for successful responses.
        /// </summary>
        public string Message { get; }

        internal static TusHandleRequestResult NotHandled { get; } = new(false, 0, null, null);

        internal static TusHandleRequestResult FromResponse(Adapters.ResponseAdapter response)
            => new(true, response.Status, response.Headers, response.Message);

        private TusHandleRequestResult(
            bool isHandled,
            HttpStatusCode statusCode,
            IReadOnlyDictionary<string, string> headers,
            string message
        )
        {
            IsHandled = isHandled;
            StatusCode = statusCode;
            Headers = headers ?? new Dictionary<string, string>();
            Message = message;
        }
    }
}
