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

        private readonly CreateOnceFactory<Tus2Storage> _storageFactory;
        private readonly CreateOnceFactory<IOngoingUploadManager> _uploadManagerFactory;

        private readonly Dictionary<string, CreateOnceFactory<Tus2Storage>> _namedStorage;
        private readonly Dictionary<string, CreateOnceFactory<IOngoingUploadManager>> _namedUploadManager;

        public Tus2ConfigurationManager(TusServiceBuilder builder, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;

            _storageFactory = CreateOnceFactory<Tus2Storage>.Create(builder.StorageFactory);
            _uploadManagerFactory = CreateOnceFactory<IOngoingUploadManager>.Create(builder.UploadManagerFactory);

            _namedStorage = builder.NamedStorage.ToDictionary(k => k.Key, v => CreateOnceFactory<Tus2Storage>.Create(v.Value));
            _namedUploadManager = builder.NamedUploadManager.ToDictionary(k => k.Key, v => CreateOnceFactory<IOngoingUploadManager>.Create(v.Value));
        }

        public async Task<Tus2Storage> GetDefaultStorage()
        {
            if (_storageFactory != null)
            {
                return await _storageFactory.Create(_httpContextAccessor.HttpContext);
            }

            return _serviceProvider.GetRequiredService<Tus2Storage>();
        }

        public async Task<Tus2Storage> GetNamedStorage(string name)
        {
            var storeFactory = _namedStorage[name];
            return await storeFactory.Create(_httpContextAccessor.HttpContext);
        }

        public async Task<IOngoingUploadManager> GetDefaultUploadManager()
        {
            if (_uploadManagerFactory != null)
            {
                return await _uploadManagerFactory.Create(_httpContextAccessor.HttpContext);
            }

            return _serviceProvider.GetRequiredService<IOngoingUploadManager>();
        }

        public async Task<IOngoingUploadManager> GetNamedUploadManager(string name)
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
