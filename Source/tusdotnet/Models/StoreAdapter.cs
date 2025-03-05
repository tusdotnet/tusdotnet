using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models.Concatenation;
#if pipelines
using System.IO.Pipelines;
#endif

namespace tusdotnet.Models
{
    /// <summary>
    /// Adapter for ITusStore
    /// </summary>
    internal sealed class StoreAdapter
        : ITusStore,
            ITusCreationStore,
            ITusExpirationStore,
            ITusChecksumStore,
            ITusConcatenationStore,
            ITusCreationDeferLengthStore,
            ITusTerminationStore,
            ITusReadableStore
#if pipelines
            ,
            ITusPipelineStore
#endif
    {
        private readonly ITusStore _store;
        private readonly ITusCreationStore _creationStore;
        private readonly ITusExpirationStore _expirationStore;
        private readonly ITusChecksumStore _checksumStore;
        private readonly ITusConcatenationStore _concatStore;
        private readonly ITusCreationDeferLengthStore _creationDeferLengthStore;
        private readonly ITusTerminationStore _terminationStore;

        private readonly ITusReadableStore _readableStore;
#if pipelines
        private readonly ITusPipelineStore _pipelineStore;
#endif

        private ITusCreationStore CreationStore
        {
            get
            {
                EnsureStoreNotNull(_creationStore);
                return _creationStore;
            }
        }
        private ITusExpirationStore ExpirationStore
        {
            get
            {
                EnsureStoreNotNull(_expirationStore);
                return _expirationStore;
            }
        }
        private ITusChecksumStore ChecksumStore
        {
            get
            {
                EnsureStoreNotNull(_checksumStore);
                return _checksumStore;
            }
        }
        private ITusConcatenationStore ConcatenationStore
        {
            get
            {
                EnsureStoreNotNull(_concatStore);
                return _concatStore;
            }
        }
        private ITusCreationDeferLengthStore CreationDeferLengthStore
        {
            get
            {
                EnsureStoreNotNull(_creationDeferLengthStore);
                return _creationDeferLengthStore;
            }
        }
        private ITusTerminationStore TerminationStore
        {
            get
            {
                EnsureStoreNotNull(_terminationStore);
                return _terminationStore;
            }
        }

        private ITusReadableStore ReadableStore
        {
            get
            {
                EnsureStoreNotNull(_readableStore);
                return _readableStore;
            }
        }

#if pipelines
        private ITusPipelineStore PipelineStore
        {
            get
            {
                StoreAdapter.EnsureStoreNotNull(_pipelineStore);
                return _pipelineStore;
            }
        }
#endif

        private static void EnsureStoreNotNull<TStore>(TStore store)
        {
            if (store == null)
                throw new InvalidOperationException(
                    $"The store does not implement {typeof(TStore).FullName}"
                );
        }

        /// <summary>
        /// Supported extensions of the store
        /// </summary>
        public StoreExtensions Extensions { get; }

        /// <summary>
        /// Supported features of the store
        /// </summary>
        public StoreFeatures Features { get; }

        /// <summary>
        /// The underlying store
        /// </summary>
        public ITusStore Store => _store;

        /// <summary>
        /// Initializes a new store adapter
        /// </summary>
        public StoreAdapter(ITusStore store, TusExtensions allowedExtensions)
        {
            _store = store;

            Extensions = new();
            Features = new();

            if (store is ITusCreationStore creationStore)
            {
                _creationStore = creationStore;
                Extensions.Creation = true;
                Extensions.CreationWithUpload = true;
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
#if trailingheaders
                Extensions.ChecksumTrailer = true;
#endif
            }

            if (store is ITusConcatenationStore concatStore)
            {
                _concatStore = concatStore;
                Extensions.Concatenation = true;
            }

            if (store is ITusCreationDeferLengthStore creationDeferLengthStore)
            {
                _creationDeferLengthStore = creationDeferLengthStore;
                Extensions.CreationDeferLength = true;
            }

            if (store is ITusTerminationStore terminationStore)
            {
                _terminationStore = terminationStore;
                Extensions.Termination = true;
            }

            if (store is ITusReadableStore readableStore)
            {
                _readableStore = readableStore;
                Features.Readable = true;
            }

#if pipelines
            if (store is ITusPipelineStore pipelineStore)
            {
                _pipelineStore = pipelineStore;
                Features.Pipelines = true;
            }
#endif

            if (allowedExtensions != null)
            {
                foreach (var item in allowedExtensions.Disallowed)
                {
                    Extensions.Disable(item);
                }
            }
        }

        /// <inheritdoc />
        public Task<long> AppendDataAsync(
            string fileId,
            Stream stream,
            CancellationToken cancellationToken
        )
        {
            return _store.AppendDataAsync(fileId, stream, cancellationToken);
        }

        /// <inheritdoc />
        public Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
        {
            return TerminationStore.DeleteFileAsync(fileId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<string> CreateFileAsync(
            long uploadLength,
            string metadata,
            CancellationToken cancellationToken
        )
        {
            return CreationStore.CreateFileAsync(uploadLength, metadata, cancellationToken);
        }

        /// <inheritdoc />
        public Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
        {
            return _store.FileExistAsync(fileId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
        {
            return _store.GetUploadLengthAsync(fileId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<string> GetUploadMetadataAsync(
            string fileId,
            CancellationToken cancellationToken
        )
        {
            return CreationStore.GetUploadMetadataAsync(fileId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
        {
            return _store.GetUploadOffsetAsync(fileId, cancellationToken);
        }

        /// <inheritdoc />
        public Task SetExpirationAsync(
            string fileId,
            DateTimeOffset expires,
            CancellationToken cancellationToken
        )
        {
            return ExpirationStore.SetExpirationAsync(fileId, expires, cancellationToken);
        }

        /// <inheritdoc />
        public Task<DateTimeOffset?> GetExpirationAsync(
            string fileId,
            CancellationToken cancellationToken
        )
        {
            return ExpirationStore.GetExpirationAsync(fileId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
        {
            return ExpirationStore.GetExpiredFilesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
        {
            return ExpirationStore.RemoveExpiredFilesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(
            CancellationToken cancellationToken
        )
        {
            return ChecksumStore.GetSupportedAlgorithmsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<bool> VerifyChecksumAsync(
            string fileId,
            string algorithm,
            byte[] checksum,
            CancellationToken cancellationToken
        )
        {
            return ChecksumStore.VerifyChecksumAsync(
                fileId,
                algorithm,
                checksum,
                cancellationToken
            );
        }

        /// <inheritdoc />
        public Task<FileConcat> GetUploadConcatAsync(
            string fileId,
            CancellationToken cancellationToken
        )
        {
            return ConcatenationStore.GetUploadConcatAsync(fileId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<string> CreatePartialFileAsync(
            long uploadLength,
            string metadata,
            CancellationToken cancellationToken
        )
        {
            return ConcatenationStore.CreatePartialFileAsync(
                uploadLength,
                metadata,
                cancellationToken
            );
        }

        /// <inheritdoc />
        public Task<string> CreateFinalFileAsync(
            string[] partialFiles,
            string metadata,
            CancellationToken cancellationToken
        )
        {
            return ConcatenationStore.CreateFinalFileAsync(
                partialFiles,
                metadata,
                cancellationToken
            );
        }

        /// <inheritdoc />
        public Task SetUploadLengthAsync(
            string fileId,
            long uploadLength,
            CancellationToken cancellationToken
        )
        {
            return CreationDeferLengthStore.SetUploadLengthAsync(
                fileId,
                uploadLength,
                cancellationToken
            );
        }

        /// <inheritdoc />
        public Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
        {
            return ReadableStore.GetFileAsync(fileId, cancellationToken);
        }

#if pipelines
        /// <inheritdoc />
        public Task<long> AppendDataAsync(
            string fileId,
            PipeReader pipeReader,
            CancellationToken cancellationToken
        )
        {
            return PipelineStore.AppendDataAsync(fileId, pipeReader, cancellationToken);
        }
#endif
    }
}
