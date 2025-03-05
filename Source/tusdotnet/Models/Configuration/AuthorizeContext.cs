using tusdotnet.Models.Concatenation;

namespace tusdotnet.Models.Configuration
{
    /// <summary>
    /// Context for the OnAuthorize event
    /// </summary>
    public class AuthorizeContext : ValidationContext<AuthorizeContext>
    {
        /// <summary>
        /// The intent of the request, i.e. what action the request is trying to perform
        /// </summary>
        public IntentType Intent { get; set; }

        /// <summary>
        /// File concatenation information if Intent is ConcatenateFiles otherwise null. Will be null if the client's provided concatenation data is invalid.
        /// </summary>
        public FileConcat FileConcatenation { get; set; }
    }
}
