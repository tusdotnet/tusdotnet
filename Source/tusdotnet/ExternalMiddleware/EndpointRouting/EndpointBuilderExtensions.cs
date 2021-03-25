#if endpointrouting

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public static class EndpointBuilderExtensions
    {
        public static IEndpointConventionBuilder MapTus<TController, TConfigurator>(this IEndpointRouteBuilder endpoints, string pattern)
            where TController : TusController<TConfigurator>
            where TConfigurator : ITusConfigurator
        {
            var handler = new TusProtocolHandlerEndpointBased<TController, TConfigurator>();
            return endpoints.Map(pattern + "/{TusFileId?}", handler.Invoke);
        }
    }
}

#endif