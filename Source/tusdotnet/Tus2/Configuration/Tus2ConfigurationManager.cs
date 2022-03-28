using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class Tus2ConfigurationManager : ITus2ConfigurationManager, IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly ITus2StorageFactory _storageFactory;
        private readonly IOngoingUploadManagerFactory _ongoingUploadManagerFactory;

        public Tus2ConfigurationManager(TusServiceBuilder builder, IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            _storageFactory = builder.StorageFactory;
            _ongoingUploadManagerFactory = builder.OngoingUploadManagerFactory;

        }

        public async Task<Tus2StorageFacade> GetDefaultStorage()
        {
            if (_storageFactory != null)
            {
                return await _storageFactory.CreateDefaultStorage();
            }

            return _serviceProvider.GetRequiredService<Tus2StorageFacade>();
        }

        // TODO: Merge this and GetDefaultStorage?
        // Make the storage factory always require a name and fallback to the default one if it returns null or something?
        public async Task<Tus2StorageFacade> GetNamedStorage(string name)
        {
            return await _storageFactory.CreateNamedStorage(name);

        }

        public async Task<IOngoingUploadManager> GetDefaultUploadManager()
        {
            if (_ongoingUploadManagerFactory != null)
            {
                return await _ongoingUploadManagerFactory.CreateOngoingUploadManager();
            }

            return _serviceProvider.GetRequiredService<IOngoingUploadManager>();
        }

        public async ValueTask DisposeAsync()
        {
            if (_storageFactory is IAsyncDisposable asyncDisposableStorageFactory)
            {
                await asyncDisposableStorageFactory.DisposeAsync();
            }

            if (_ongoingUploadManagerFactory is IAsyncDisposable asyncDisposableOngoingUploadManagerFactory)
            {
                await asyncDisposableOngoingUploadManagerFactory.DisposeAsync();
            }
        }
    }
}
