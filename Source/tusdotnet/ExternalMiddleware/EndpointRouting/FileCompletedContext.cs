#if endpointrouting

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public class FileCompletedContext
    {
        public string FileId { get; internal set; }
    }
}

#endif