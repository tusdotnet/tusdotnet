using Microsoft.AspNetCore.Http;
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

            if (configure != null)
            {
                var builder = new TusServiceBuilder();
                configure(builder);

                if (builder.UploadManager != null)
                    services.AddScoped(_ => builder.UploadManager);

                if (builder.Storage != null)
                    services.AddScoped(_ => builder.Storage);

                foreach (var handler in builder.Handlers)
                {
                    services.AddTransient(handler);
                }

                services.AddScoped<ITus2ConfigurationManager>(provider =>
                {
                    return new Tus2ConfigurationManager(builder, provider, provider.GetRequiredService<IHttpContextAccessor>());
                });
            }

            return services;
        }
    }
}
