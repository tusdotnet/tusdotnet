#if endpointrouting

using Microsoft.Extensions.DependencyInjection;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public sealed class TusServiceCollection
    {
        public IServiceCollection Services { get; }

        internal TusServiceCollection(IServiceCollection services)
        {
            Services = services;
        }

        public TusServiceCollection AddController<TController, TConfigurator>()
            where TController : TusController<TConfigurator>
            where TConfigurator : class, ITusConfigurator
        {
            Services.AddTransient<TController, TController>();

            return this;
        }

        public TusServiceCollection AddConfigurator<TConfigurator>() where TConfigurator : class, ITusConfigurator
        {
            Services.AddScoped<TConfigurator, TConfigurator>();
            Services.AddScoped<StorageService<TConfigurator>, StorageService<TConfigurator>>();

            return this;
        }
    }
}

#endif