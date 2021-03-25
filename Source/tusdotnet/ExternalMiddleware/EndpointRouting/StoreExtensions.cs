#if endpointrouting

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public class StoreExtensions
    {
        public bool Creation { get; set; }

        public bool Expiration { get; set; }

        public bool Checksum { get; set; }
    }
}

#endif