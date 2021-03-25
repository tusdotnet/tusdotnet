#if endpointrouting

using Microsoft.AspNetCore.Builder;
using System;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public class TusEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public void Add(Action<EndpointBuilder> convention)
        {
            // Do nothing for now
        }
    }
}

#endif