namespace tusdotnet.Models
{
    /// <summary>
    /// Exception thrown by a store when another request modified the upload during the current write,
    /// e.g. a conditional write against the storage failed or the file lock was lost.
    /// The store must not persist the current request's data before throwing it.
    /// All TusUploadConflictExceptions result in a 409 Conflict response with the exception message
    /// as the response body. The client then sends HEAD and resumes from the current offset.
    /// </summary>
    public class TusUploadConflictException : TusStoreException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TusUploadConflictException"/> class.
        /// </summary>
        /// <param name="message">The message. This message will be returned to the client.</param>
        public TusUploadConflictException(string message)
            : base(message)
        {
            // Left blank.
        }
    }
}
