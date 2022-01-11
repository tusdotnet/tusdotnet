using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class Tus2ConfigurationManager : ITus2ConfigurationManager, IAsyncDisposable
    {
        private readonly TusServiceBuilder _builder;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly LinkedList<IDisposable> _disposables;
        private readonly LinkedList<IAsyncDisposable> _asyncDisposables;

        public Tus2ConfigurationManager(TusServiceBuilder builder, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _builder = builder;
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
            _disposables = new();
            _asyncDisposables = new();
        }

        public async Task<ITus2Storage> GetDefaultStorage()
        {
            var storageFactory = _serviceProvider.GetService<Func<HttpContext, Task<ITus2Storage>>>();
            if (storageFactory != null)
            {
                var store = await storageFactory(_httpContextAccessor.HttpContext);
                AddDisposable(store);
                return store;
            }

            return _serviceProvider.GetRequiredService<ITus2Storage>();
        }

        public async Task<ITus2Storage> GetNamedStorage(string name)
        {
            var storeFactory = _builder.NamedStorage[name];

            var store = await storeFactory(_httpContextAccessor.HttpContext);
            AddDisposable(store);
            return store;
        }

        public async Task<IUploadManager> GetDefaultUploadManager()
        {
            var factory = _serviceProvider.GetService<Func<HttpContext, Task<IUploadManager>>>();
            if (factory != null)
            {
                var uploadManager = await factory(_httpContextAccessor.HttpContext);
                AddDisposable(uploadManager);
                return uploadManager;
            }

            return _serviceProvider.GetRequiredService<IUploadManager>();
        }

        public async Task<IUploadManager> GetNamedUploadManager(string name)
        {
            var uploadManagerFactory = _builder.NamedUploadManager[name];
            AddDisposable(uploadManagerFactory);
            return await uploadManagerFactory(_httpContextAccessor.HttpContext);
        }

        private void AddDisposable(object obj)
        {
            if (obj is IDisposable disposable)
                _disposables.AddLast(disposable);

            if (obj is IAsyncDisposable asyncDisposable)
                _asyncDisposables.AddLast(asyncDisposable);
        }

        public async ValueTask DisposeAsync()
        {
            while (_asyncDisposables.First != null)
            {
                await _asyncDisposables.First.Value.DisposeAsync();
                _asyncDisposables.RemoveFirst();
            }

            while (_disposables.First != null)
            {
                _disposables.First.Value.Dispose();
                _disposables.RemoveFirst();
            }
        }
    }
}
