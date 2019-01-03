using System.Net;

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Base context for all contexts that can be validated
    /// </summary>
    /// <typeparam name="TSelf">The type of the derived class inheriting the EventContext</typeparam>
    public abstract class ValidationContext<TSelf> : EventContext<TSelf> where TSelf : EventContext<TSelf>, new()
    {
        /// <summary>
        /// Error message set using <code>FailRequest</code>
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Http status code set using <code>FailRequest</code>. Defaults to 400 (Bad request).
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// True if <code>FailRequest</code> has been called
        /// </summary>
        public bool HasFailed { get; private set; }

        /// <summary>
        /// Call this method to fail the validation of the context and cause tusdotnet to return a 400 Bad Request error to the client.
        /// Calling this method multiple times will concatenate the messages.
        /// </summary>
        /// <param name="message">The error message to return to the client</param>
        public void FailRequest(string message)
        {
            FailRequest(HttpStatusCode.BadRequest, message);
        }

        /// <summary>
        /// Call this method to fail the validation of the context and cause tusdotnet to return an error to the client.
        /// Calling this method multiple times will override the status code being returned.
        /// </summary>
        /// <param name="statusCode">The http status code to return to the client</param>
        public void FailRequest(HttpStatusCode statusCode)
        {
            FailRequest(statusCode, null);
        }

        /// <summary>
        /// Call this method to fail the validation of the context and cause tusdotnet to return an error to the client.
        /// Calling this method multiple times will override the status code and concatenate the message being returned.
        /// </summary>
        /// <param name="statusCode">The http status code to return to the client</param>
        /// <param name="message">The error message to return to the client</param>
        public void FailRequest(HttpStatusCode statusCode, string message)
        {
            HasFailed = true;
            StatusCode = statusCode;
            if (message != null)
            {
                ErrorMessage += message;
            }
        }
    }
}