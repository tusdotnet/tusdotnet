#if endpointrouting

using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public interface ITusConfigurator
    {
        Task<EndpointOptions> Configure(HttpContext context);
    }
}

#endif