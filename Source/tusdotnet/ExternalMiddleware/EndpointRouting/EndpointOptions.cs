#if endpointrouting

using System;
using tusdotnet.Interfaces;
using tusdotnet.Models.Expiration;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public class EndpointOptions
    {
        public ITusStore Store { get; set; }

        public ExpirationBase Expiration { get; set; }

        private DateTimeOffset? _systemTime;

        internal void MockSystemTime(DateTimeOffset systemTime)
        {
            _systemTime = systemTime;
        }

        internal DateTimeOffset GetSystemTime()
        {
            return _systemTime ?? DateTimeOffset.UtcNow;
        }
    }
}

#endif