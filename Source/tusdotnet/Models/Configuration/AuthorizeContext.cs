namespace tusdotnet.Models.Configuration
{
    public class AuthorizeContext : ValidationContext<AuthorizeContext>
    {
        public IntentType Intent { get; set; }
    }
}