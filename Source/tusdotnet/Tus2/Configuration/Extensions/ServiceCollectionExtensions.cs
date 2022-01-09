using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace tusdotnet.Tus2
{
    public static class ServiceCollectionExtensions
    {
        public static TusServiceBuilder AddTus2(this IServiceCollection services, Action<Tus2Options> configure = null)
        {
            services.AddSingleton<IUploadTokenParser, UploadTokenParser>();
            services.AddSingleton<IMetadataParser, MetadataParser>();

            services.AddHttpContextAccessor();
            services.AddSingleton<ITus2ConfigurationManager, Tus2ConfigurationManager>();

            configure ??= DefaultConfig();

            services.Configure(configure);

            return new(services);
        }

        private static Action<Tus2Options> DefaultConfig()
        {
            return options =>
            {
                options.AllowClientToDeleteFile = true;
            };
        }
    }

    public class TusServiceBuilder
    {
        public IServiceCollection Services { get; set; }

        public TusServiceBuilder(IServiceCollection services)
        {
            Services = services;
        }

        // Possibly use? 
        /*
         * services.AddTransient<ServiceA>();
services.AddTransient<ServiceB>();
services.AddTransient<ServiceC>();

services.AddTransient<ServiceResolver>(serviceProvider => key =>
{
    switch (key)
    {
        case "A":
            return serviceProvider.GetService<ServiceA>();
        case "B":
            return serviceProvider.GetService<ServiceB>();
        case "C":
            return serviceProvider.GetService<ServiceC>();
        default:
            throw new KeyNotFoundException(); // or maybe return null, up to you
    }
});
         * 
         * 
         * */

        // TODO: Factories should probably be singleton.
        // TODO: Add support for using the service provider, e.g. serviceProvder => store|storeFactory

        public TusServiceBuilder AddStorage(ITus2Store store)
        {
            Services.AddScoped(_ => store);

            return this;
        }

        public TusServiceBuilder AddStorage(ITus2StoreFactory factory)
        {
            Services.AddScoped(_ => factory);

            return this;
        }

        public TusServiceBuilder AddStorage(string name, ITus2Store store)
        {
            return AddStorage(name, new SingleStoreFactory(store));
        }

        public TusServiceBuilder AddStorage(string name, ITus2StoreFactory factory)
        {
            Services.AddSingleton(new NamedFactory<ITus2StoreFactory>
            {
                Name = name,
                Factory = factory
            });

            return this;
        }

        public TusServiceBuilder AddUploadManager(IUploadManager uploadManager)
        {
            Services.AddScoped(_ => uploadManager);

            return this;
        }

        public TusServiceBuilder AddUploadManagerFactory(IUploadManagerFactory uploadManagerFactory)
        {
            Services.AddScoped(_ => uploadManagerFactory);

            return this;
        }

        public TusServiceBuilder AddUploadManager(string name, IUploadManager uploadManager)
        {
            return AddUploadManager(name, new SingleUploadManagerFactory(uploadManager));
        }

        public TusServiceBuilder AddUploadManager(string name, IUploadManagerFactory uploadManagerFactory)
        {
            Services.AddSingleton(new NamedFactory<IUploadManagerFactory>
            {
                Name = name,
                Factory = uploadManagerFactory
            });

            return this;
        }

        public TusServiceBuilder AddHandler<T>() where T : TusHandler
        {
            Services.AddTransient<T>();

            return this;
        }
    }
}
