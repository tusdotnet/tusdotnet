using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTus2(this IServiceCollection services, Action<TusServiceBuilder> configure = null)
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<IUploadTokenParser, UploadTokenParser>();
            services.AddSingleton<IMetadataParser, DefaultMetadataParser>();

            if (configure != null)
            {
                var builder = new TusServiceBuilder();
                configure(builder);

                if (builder.UploadManager != null)
                    services.AddSingleton(builder.UploadManager);

                if (builder.UploadManagerFactory != null)
                    services.AddScoped(_ => builder.UploadManagerFactory);

                if (builder.Storage != null)
                    services.AddScoped(_ => builder.Storage);

                if (builder.StorageFactory != null)
                    services.AddScoped(_ => builder.StorageFactory);

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

    public class TusServiceBuilder
    {
        public Dictionary<string, Func<HttpContext, Task<ITus2Storage>>> NamedStorage { get; }

        public Dictionary<string, Func<HttpContext, Task<IUploadManager>>> NamedUploadManager { get; }

        public ITus2Storage Storage { get; private set; }

        public Func<HttpContext, Task<ITus2Storage>> StorageFactory { get; private set; }

        public IUploadManager UploadManager { get; private set; }

        public Func<HttpContext, Task<IUploadManager>> UploadManagerFactory { get; private set; }

        public LinkedList<Type> Handlers { get; private set; }

        public TusServiceBuilder()
        {
            NamedStorage = new();
            NamedUploadManager = new();
            Handlers = new();
        }

        public TusServiceBuilder AddStorage(ITus2Storage storageInstance)
        {
            Storage = storageInstance;
            return this;
        }

        public TusServiceBuilder AddStorage(Func<HttpContext, Task<ITus2Storage>> storageFactory)
        {
            StorageFactory = storageFactory;
            return this;
        }

        public TusServiceBuilder AddStorage(string name, ITus2Storage storageInstance)
        {
            NamedStorage.Add(name, _ => Task.FromResult(storageInstance));
            return this;
        }

        public TusServiceBuilder AddUploadManager(IUploadManager uploadManager)
        {
            UploadManager = uploadManager;
            return this;
        }

        public TusServiceBuilder AddUploadManager(Func<HttpContext, Task<IUploadManager>> uploadManagerFactory)
        {
            UploadManagerFactory = uploadManagerFactory;
            return this;
        }

        public TusServiceBuilder AddUploadManager(string name, IUploadManager uploadManager)
        {
            NamedUploadManager.Add(name, _ => Task.FromResult(uploadManager));
            return this;
        }

        public TusServiceBuilder AddUploadManager(string name, Func<HttpContext, Task<IUploadManager>> uploadManagerFactory)
        {
            NamedUploadManager.Add(name, uploadManagerFactory);
            return this;
        }

        public TusServiceBuilder AddHandler<T>() where T : TusHandler
        {
            Handlers.AddLast(typeof(T));
            return this;
        }
    }
}
