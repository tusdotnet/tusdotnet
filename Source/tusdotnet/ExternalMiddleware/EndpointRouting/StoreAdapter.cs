#if endpointrouting

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    public sealed class StoreAdapter : ITusStore, ITusCreationStore, ITusExpirationStore, ITusChecksumStore
    {
        private readonly ITusStore _store;
        // TODO: Make into a property and require the property to be set and throw an invalid operation exception otherwise? Prevents null refs.
        private readonly ITusCreationStore _creationStore;
        private readonly ITusExpirationStore _expirationStore;
        private readonly ITusChecksumStore _checksumStore;

        public StoreExtensions Extensions { get; }

        public StoreAdapter(ITusStore store)
        {
            _store = store;

            Extensions = new();

            if (store is ITusCreationStore creationStore)
            {
                _creationStore = creationStore;
                Extensions.Creation = true;
            }

            if (store is ITusExpirationStore expirationStore)
            {
                _expirationStore = expirationStore;
                Extensions.Expiration = true;
            }

            if (store is ITusChecksumStore checksumStore)
            {
                _checksumStore = checksumStore;
                Extensions.Checksum = true;
            }
        }

        public Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
        {
            return _store.AppendDataAsync(fileId, stream, cancellationToken);
        }

        public Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            return _creationStore.CreateFileAsync(uploadLength, metadata, cancellationToken);
        }

        public Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
        {
            return _store.FileExistAsync(fileId, cancellationToken);
        }

        public Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
        {
            return _store.GetUploadLengthAsync(fileId, cancellationToken);
        }

        public Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
        {
            return _creationStore.GetUploadMetadataAsync(fileId, cancellationToken);
        }

        public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
        {
            return _store.GetUploadOffsetAsync(fileId, cancellationToken);
        }

        public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        {
            return _expirationStore.SetExpirationAsync(fileId, expires, cancellationToken);
        }

        public Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
        {
            return _expirationStore.GetExpirationAsync(fileId, cancellationToken);
        }

        public Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
        {
            return _expirationStore.GetExpiredFilesAsync(cancellationToken);
        }

        public Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
        {
            return _expirationStore.RemoveExpiredFilesAsync(cancellationToken);
        }

        public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken)
        {
            return _checksumStore.GetSupportedAlgorithmsAsync(cancellationToken);
        }

        public Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)
        {
            return _checksumStore.VerifyChecksumAsync(fileId, algorithm, checksum, cancellationToken);
        }
    }
}

#endif