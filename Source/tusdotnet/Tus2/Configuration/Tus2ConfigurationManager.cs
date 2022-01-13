using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class Tus2ConfigurationManager : ITus2ConfigurationManager, IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly CreateOnceFactory<ITus2Storage> _storageFactory;
        private readonly CreateOnceFactory<IUploadManager> _uploadManagerFactory;

        private readonly Dictionary<string, CreateOnceFactory<ITus2Storage>> _namedStorage;
        private readonly Dictionary<string, CreateOnceFactory<IUploadManager>> _namedUploadManager;

        public Tus2ConfigurationManager(TusServiceBuilder builder, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;

            _storageFactory = new(builder.StorageFactory);
            _uploadManagerFactory = new(builder.UploadManagerFactory);

            _namedStorage = builder.NamedStorage.ToDictionary(k => k.Key, v => new CreateOnceFactory<ITus2Storage>(v.Value));
            _namedUploadManager = builder.NamedUploadManager.ToDictionary(k => k.Key, v => new CreateOnceFactory<IUploadManager>(v.Value));
        }

        public async Task<ITus2Storage> GetDefaultStorage()
        {
            if (_storageFactory != null)
            {
                return await _storageFactory.Create(_httpContextAccessor.HttpContext);
            }

            return _serviceProvider.GetRequiredService<ITus2Storage>();
        }

        public async Task<ITus2Storage> GetNamedStorage(string name)
        {
            var storeFactory = _namedStorage[name];
            return await storeFactory.Create(_httpContextAccessor.HttpContext);
        }

        public async Task<IUploadManager> GetDefaultUploadManager()
        {
            if (_uploadManagerFactory != null)
            {
                return await _uploadManagerFactory.Create(_httpContextAccessor.HttpContext);
            }

            return _serviceProvider.GetRequiredService<IUploadManager>();
        }

        public async Task<IUploadManager> GetNamedUploadManager(string name)
        {
            var uploadManagerFactory = _namedUploadManager[name];
            return await uploadManagerFactory.Create(_httpContextAccessor.HttpContext);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var item in _namedStorage)
            {
                await item.Value.DisposeAsync();
            }

            foreach (var item in _namedUploadManager)
            {
                await item.Value.DisposeAsync();
            }
        }
    }
}
