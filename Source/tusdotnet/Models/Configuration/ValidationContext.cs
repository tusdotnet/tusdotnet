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
        /// True if <code>FailRequest</code> has been called
        /// </summary>
        public bool HasFailed => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Call this method to fail the validation of the context and cause tusdotnet to return an error to the client.
        /// Calling this method multiple times will concatenate the messages.
        /// </summary>
        /// <param name="message">The error message to return to the client</param>
        public void FailRequest(string message)
        {
#warning TODO Add support for http status responses with and without a message (to support OnAuthorize)
            ErrorMessage += message;
        }
    }
}