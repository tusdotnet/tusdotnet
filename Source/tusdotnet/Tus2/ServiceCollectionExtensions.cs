using Microsoft.Extensions.DependencyInjection;
using System;

namespace tusdotnet.Tus2
{
    public static class ServiceCollectionExtensions
    {
        public static TusServiceBuilder AddTus(this IServiceCollection services)
        {
            services.AddSingleton<IUploadTokenParser, UploadTokenParser>();
            services.AddSingleton<IMetadataParser, MetadataParser>();

            return new(services);
        }
    }

    public class TusServiceBuilder
    {
        private readonly IServiceCollection _services;

        public TusServiceBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public TusServiceBuilder Configure(Action<Tus2Options> options)
        {
            _services.Configure(options);

            return this;
        }

        public TusServiceBuilder WithController<T>() where T : TusBaseHandler
        {
            _services.AddTransient<T>();

            return this;
        }
    }
}
