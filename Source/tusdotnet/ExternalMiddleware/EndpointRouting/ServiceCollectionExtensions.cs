#if endpointrouting

using Microsoft.Extensions.DependencyInjection;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public static class ServiceCollectionExtensions
    {
        public static TusServiceCollection AddTus(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            return new TusServiceCollection(services);
        }
    }
}

#endif