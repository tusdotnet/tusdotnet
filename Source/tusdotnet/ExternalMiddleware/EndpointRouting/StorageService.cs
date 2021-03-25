#if endpointrouting

using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;
using tusdotnet.Models.Expiration;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public sealed class StorageService<TTusConfigurator> where TTusConfigurator : ITusConfigurator
    {
        private StoreAdapter _storeAdapter;
        private EndpointOptions _options;
        private readonly TTusConfigurator _configurator;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public StorageService(TTusConfigurator configurator, IHttpContextAccessor httpContextAccessor)
        {
            _configurator = configurator;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<StoreAdapter> GetStore()
        {
            await LoadOptions();
            return _storeAdapter;
        }

        public async Task Create(CreateContext context, CancellationToken cancellationToken)
        {
            await LoadOptions();

            context.FileId = await _storeAdapter.CreateFileAsync(context.UploadLength, context.UploadMetadata, cancellationToken);

            if (_storeAdapter.Extensions.Expiration && _options.Expiration != null)
            {
                // Expiration is only used when patching files so if the file is not empty and we did not have any data in the current request body,
                // we need to update the header here to be able to keep track of expiration for this file.
                context.FileExpires = _options.GetSystemTime().Add(_options.Expiration.Timeout);
                await _storeAdapter.SetExpirationAsync(context.FileId, context.FileExpires.Value, cancellationToken);
            }
        }

        public async Task Write(WriteContext context, CancellationToken cancellationToken)
        {
            await LoadOptions();

            var guardedStream = new ClientDisconnectGuardedReadOnlyStream(context.RequestStream, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            var bytesWritten = await _storeAdapter.AppendDataAsync(context.FileId, guardedStream, guardedStream.CancellationToken);

            context.UploadOffset += bytesWritten;

            if (!_storeAdapter.Extensions.Expiration)
                return;

            if (_options.Expiration is SlidingExpiration)
            {
                context.FileExpires = _options.GetSystemTime().Add(_options.Expiration.Timeout);
                await _storeAdapter.SetExpirationAsync(context.FileId, context.FileExpires.Value, cancellationToken);
            }
            else
            {
                context.FileExpires = await _storeAdapter.GetExpirationAsync(context.FileId, cancellationToken);
            }

            if (!_storeAdapter.Extensions.Checksum)
                return;

            var checksum = context.GetChecksumProvidedByClient();

            if (checksum != null)
                context.ChecksumMatchesTheOneProvidedByClient = await _storeAdapter.VerifyChecksumAsync(context.FileId, checksum.Algorithm, checksum.Hash, cancellationToken);

            context.IsComplete = await _storeAdapter.GetUploadLengthAsync(context.FileId, cancellationToken) == context.UploadOffset;
        }

        private async Task LoadOptions()
        {
            if (_options != null)
                return;

            _options = await _configurator.Configure(_httpContextAccessor.HttpContext);
            _storeAdapter = new StoreAdapter(_options.Store);
        }
    }
}

#endif