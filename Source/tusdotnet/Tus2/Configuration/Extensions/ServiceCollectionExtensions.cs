using Microsoft.Extensions.DependencyInjection;
using System;

namespace tusdotnet.Tus2
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTus2(this IServiceCollection services, Action<TusServiceBuilder> configure = null)
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<IUploadTokenParser, UploadTokenParser>();
            services.AddSingleton<IMetadataParser, DefaultMetadataParser>();
            services.AddSingleton<IOngoingUploadManager, OngoingUploadManagerInMemory>();
            services.AddScoped<IHeaderParser, Tus2HeadersParser>();

            if (configure != null)
            {
                var builder = new TusServiceBuilder();
                configure(builder);

                AddOngoingUploadManager(services, builder);
                AddStorage(services, builder);
                AddHandlers(services, builder);
                AddConfigurationManager(services, builder);
            }

            return services;
        }

        private static void AddConfigurationManager(IServiceCollection services, TusServiceBuilder builder)
        {
            services.AddScoped<ITus2ConfigurationManager>(provider =>
            {
                return new Tus2ConfigurationManager(builder, provider);
            });
        }

        private static void AddHandlers(IServiceCollection services, TusServiceBuilder builder)
        {
            foreach (var handler in builder.Handlers)
            {
                services.AddTransient(handler);
            }
        }

        private static void AddOngoingUploadManager(IServiceCollection services, TusServiceBuilder builder)
        {
            if (builder.OngoingUploadManager != null)
                services.AddScoped(_ => builder.OngoingUploadManager);

            if (builder.OngoingUploadManagerFactory != null)
            {
                services.AddScoped(_ => builder.OngoingUploadManagerFactory);
            }
        }

        private static void AddStorage(IServiceCollection services, TusServiceBuilder builder)
        {
            if (builder.Storage != null)
            {
                services.AddScoped(_ => builder.Storage);
                services.AddScoped(_ => new Tus2StorageFacade(builder.Storage));
            }

            if (builder.StorageFactory != null)
            {
                services.AddScoped(_ => builder.StorageFactory);
            }
        }
    }
}
