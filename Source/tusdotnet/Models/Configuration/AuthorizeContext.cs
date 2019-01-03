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
    }
}