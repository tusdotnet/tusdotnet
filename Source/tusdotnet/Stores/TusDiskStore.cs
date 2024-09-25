﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Stores.FileIdProviders;

namespace tusdotnet.Stores
{
    /// <summary>
    /// The built in data store that save files on disk.
    /// </summary>
    public partial class TusDiskStore :
        ITusStore,
#if pipelines
        ITusPipelineStore,
#endif
        ITusCreationStore,
        ITusReadableStore,
        ITusTerminationStore,
        ITusChecksumStore,
        ITusConcatenationStore,
        ITusExpirationStore,
        ITusCreationDeferLengthStore
    {
        private readonly string _directoryPath;
        private readonly bool _deletePartialFilesOnConcat;
        private readonly InternalFileRep.FileRepFactory _fileRepFactory;
        private readonly ITusFileIdProvider _fileIdProvider;

        // These are the read and write buffer sizes, they will get the value of TusDiskBufferSize.Default if not set in the constructor.
        private readonly int _maxReadBufferSize;
        private readonly int _maxWriteBufferSize;

        // Use our own array pool to not leak data to other parts of the running app.
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create();

        private static readonly GuidFileIdProvider _defaultFileIdProvider = new();

        private static readonly IEnumerable<string> _supportedAlgorithms = new[] { "sha1" };

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// Using this overload will not delete partial files if a final concatenation is performed.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        public TusDiskStore(string directoryPath) : this(directoryPath, false, TusDiskBufferSize.Default)
        {
            // Left blank.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat) 
            : this(directoryPath, deletePartialFilesOnConcat, TusDiskBufferSize.Default)
        {
            // Left blank.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        /// <param name="bufferSize">The buffer size to use when reading and writing. If unsure use <see cref="TusDiskBufferSize.Default"/>.</param>
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat, TusDiskBufferSize bufferSize) 
            : this(directoryPath, deletePartialFilesOnConcat, bufferSize, _defaultFileIdProvider)
        {
            // Left blank.
        }

#if NETCOREAPP2_0_OR_GREATER
        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        /// <param name="bufferSize">The buffer size to use when reading and writing. If unsure use <see cref="TusDiskBufferSize.Default"/>.</param>
        /// <param name="fileIdProvider">The provider that generates ids for files. If unsure use <see cref="GuidFileIdProvider"/>.</param>
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat, TusDiskBufferSize bufferSize, ITusFileIdProvider fileIdProvider)
            : this(directoryPath, deletePartialFilesOnConcat, bufferSize, fileIdProvider, _defaultFileSystem)
        {
        }
#else
        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        /// <param name="bufferSize">The buffer size to use when reading and writing. If unsure use <see cref="TusDiskBufferSize.Default"/>.</param>
        /// <param name="fileIdProvider">The provider that generates ids for files. If unsure use <see cref="GuidFileIdProvider"/>.</param>
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat, TusDiskBufferSize bufferSize, ITusFileIdProvider fileIdProvider)
        {
            _directoryPath = directoryPath;
            _deletePartialFilesOnConcat = deletePartialFilesOnConcat;
            _fileRepFactory = new InternalFileRep.FileRepFactory(_directoryPath);

            if (bufferSize == null)
                bufferSize = TusDiskBufferSize.Default;

            _maxWriteBufferSize = bufferSize.WriteBufferSizeInBytes;
            _maxReadBufferSize = bufferSize.ReadBufferSizeInBytes;

            _fileIdProvider = fileIdProvider;
        }
#endif
        
        /// <inheritdoc />
        public async Task<bool> FileExistAsync(string fileId, CancellationToken _)
        {
            return _fileRepFactory.Data(await InternalFileId.Parse(_fileIdProvider, fileId)).Exist();
        }

        /// <inheritdoc />
        public async Task<long?> GetUploadLengthAsync(string fileId, CancellationToken _)
        {
            var firstLine = _fileRepFactory.UploadLength(await InternalFileId.Parse(_fileIdProvider, fileId)).ReadFirstLine(true);
            return firstLine == null
                ? (long?)null
                : long.Parse(firstLine);
        }

        /// <inheritdoc />
        public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken _)
        {
            return _fileRepFactory.Data(await InternalFileId.Parse(_fileIdProvider, fileId)).GetLength();
        }

        /// <inheritdoc />
        public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var fileId = await InternalFileId.CreateNew(_fileIdProvider, metadata);

#if NETCOREAPP2_0_OR_GREATER
            await _fileSystem.FileStream.New(_fileRepFactory.Data(fileId).Path, FileMode.CreateNew).DisposeAsync();
#else
            new FileStream(_fileRepFactory.Data(fileId).Path, FileMode.CreateNew).Dispose();
#endif

            if (uploadLength != -1)
            {
                await SetUploadLengthAsync(fileId, uploadLength, cancellationToken);
            }
            _fileRepFactory.Metadata(fileId).Write(metadata);
            return fileId;
        }

        /// <inheritdoc />
        public async Task<string> GetUploadMetadataAsync(string fileId, CancellationToken _)
        {
            var firstLine = _fileRepFactory.Metadata(await InternalFileId.Parse(_fileIdProvider, fileId)).ReadFirstLine(true);
            return string.IsNullOrEmpty(firstLine) ? null : firstLine;
        }

        /// <inheritdoc />
        public async Task<ITusFile> GetFileAsync(string fileId, CancellationToken _)
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);
            var data = _fileRepFactory.Data(internalFileId);

            return data.Exist()
                ? new TusDiskFile(data, _fileRepFactory.Metadata(internalFileId))
                : null;
        }

        /// <inheritdoc />
        public async Task DeleteFileAsync(string fileId, CancellationToken _)
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);
            await Task.Run(() =>
            {
                _fileRepFactory.Data(internalFileId).Delete();
                _fileRepFactory.UploadLength(internalFileId).Delete();
                _fileRepFactory.Metadata(internalFileId).Delete();
                _fileRepFactory.UploadConcat(internalFileId).Delete();
                _fileRepFactory.ChunkStartPosition(internalFileId).Delete();
                _fileRepFactory.ChunkComplete(internalFileId).Delete();
                _fileRepFactory.Expiration(internalFileId).Delete();
            });
        }

        
        /// <inheritdoc />
        public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken _)
        {
            return Task.FromResult(_supportedAlgorithms);
        }

        /// <inheritdoc />
        public async Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken _)
        {
            var valid = false;
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);
            using (var dataStream = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                var chunkPositionFile = _fileRepFactory.ChunkStartPosition(internalFileId);
                var chunkStartPosition = chunkPositionFile.ReadFirstLineAsLong(true, 0);
                var chunkCompleteFile = _fileRepFactory.ChunkComplete(internalFileId);

                // Only verify the checksum if the entire lastest chunk has been written.
                // If not, just discard the last chunk as it won't match the checksum anyway.
                // If the client has provided a faulty checksum-trailer we should also just discard the chunk.
                if (chunkCompleteFile.Exist() && !ChecksumTrailerHelper.IsFallback(algorithm, checksum))
                {
                    var calculateSha1 = dataStream.CalculateSha1(chunkStartPosition);
                    valid = checksum.SequenceEqual(calculateSha1);
                }

                if (!valid)
                {
                    dataStream.Seek(0, SeekOrigin.Begin);
                    dataStream.SetLength(chunkStartPosition);
                }
            }

            return valid;
        }

        /// <inheritdoc />
        public async Task<FileConcat> GetUploadConcatAsync(string fileId, CancellationToken _)
        {
            var firstLine = _fileRepFactory.UploadConcat(await InternalFileId.Parse(_fileIdProvider, fileId)).ReadFirstLine(true);
            return string.IsNullOrWhiteSpace(firstLine)
                ? null
                : new UploadConcat(firstLine).Type;
        }

        /// <inheritdoc />
        public async Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var fileId = await CreateFileAsync(uploadLength, metadata, cancellationToken);
            _fileRepFactory.UploadConcat(await InternalFileId.Parse(_fileIdProvider, fileId)).Write(new FileConcatPartial().GetHeader());
            return fileId;
        }

        /// <inheritdoc />
        public async Task<string> CreateFinalFileAsync(string[] partialFiles, string metadata, CancellationToken cancellationToken)
        {
            InternalFileRep[] partialInternalFileReps = new InternalFileRep[partialFiles.Length];
            for (int i = 0; i < partialFiles.Length; i++)
            {
                partialInternalFileReps[i] = _fileRepFactory.Data(await InternalFileId.Parse(_fileIdProvider, partialFiles[i]));

                if (!partialInternalFileReps[i].Exist())
                {
                    throw new TusStoreException($"File {partialFiles[i]} does not exist");
                }
            }

            var length = partialInternalFileReps.Sum(f => f.GetLength());

            var fileId = await CreateFileAsync(length, metadata, cancellationToken);

            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            _fileRepFactory.UploadConcat(internalFileId).Write(new FileConcatFinal(partialFiles).GetHeader());

            using (var finalFile = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Open, FileAccess.Write, FileShare.None))
            {
                foreach (var partialFile in partialInternalFileReps)
                {
                    using var partialStream = partialFile.GetStream(FileMode.Open, FileAccess.Read, FileShare.Read);
                    await partialStream.CopyToAsync(finalFile);
                }
            }

            if (_deletePartialFilesOnConcat)
            {
                await Task.WhenAll(partialInternalFileReps.Select(f => DeleteFileAsync(f.FileId, cancellationToken)));
            }

            return fileId;
        }

        /// <inheritdoc />
        public async Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken _)
        {
            _fileRepFactory.Expiration(await InternalFileId.Parse(_fileIdProvider, fileId)).Write(expires.ToString("O"));
        }

        /// <inheritdoc />
        public async Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken _)
        {
            var expiration = _fileRepFactory.Expiration(await InternalFileId.Parse(_fileIdProvider, fileId)).ReadFirstLine(true);

            return expiration == null
                ? null
                : DateTimeOffset.ParseExact(expiration, "O", null);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken _)
        {
            var expiredFiles = new List<string>();
            
#if NETCOREAPP2_0_OR_GREATER
            var enumerateFiles = _fileSystem.Directory.EnumerateFiles(_directoryPath, "*.expiration");
#else
            var enumerateFiles = Directory.EnumerateFiles(_directoryPath, "*.expiration");
#endif
            
            foreach (var file in enumerateFiles)
            {
#if NETCOREAPP2_0_OR_GREATER
                var fileNameWithoutExtension = _fileSystem.Path.GetFileNameWithoutExtension(file);
#else
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
#endif
                
                var f = await InternalFileId.Parse(_fileIdProvider, fileNameWithoutExtension);
                if (FileHasExpired(f, _fileRepFactory) && FileIsIncomplete(f, _fileRepFactory))
                {
                    expiredFiles.Add(f);
                }
            }

            return expiredFiles;

            static bool FileHasExpired(InternalFileId fileId, InternalFileRep.FileRepFactory fileRepFactory)
            {
                var firstLine = fileRepFactory.Expiration(fileId).ReadFirstLine();
                return !string.IsNullOrWhiteSpace(firstLine)
                       && DateTimeOffset.ParseExact(firstLine, "O", null).HasPassed();
            }

            static bool FileIsIncomplete(InternalFileId fileId, InternalFileRep.FileRepFactory fileRepFactory)
            {
                var uploadLength = fileRepFactory.UploadLength(fileId).ReadFirstLineAsLong(fileIsOptional: true, defaultValue: long.MinValue);

                if (uploadLength == long.MinValue)
                {
                    return true;
                }

                var dataFile = fileRepFactory.Data(fileId);

                if (!dataFile.Exist())
                {
                    return true;
                }

                return uploadLength != dataFile.GetLength();
            }
        }

        /// <inheritdoc />
        public async Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
        {
            var expiredFiles = await GetExpiredFilesAsync(cancellationToken);
            var deleteFileTasks = expiredFiles.Select(file => DeleteFileAsync(file, cancellationToken)).ToList();

            await Task.WhenAll(deleteFileTasks);

            return deleteFileTasks.Count;
        }

        /// <inheritdoc />
        public async Task SetUploadLengthAsync(string fileId, long uploadLength, CancellationToken _)
        {
            _fileRepFactory.UploadLength(await InternalFileId.Parse(_fileIdProvider, fileId)).Write(uploadLength.ToString());
        }

        private InternalFileRep InitializeChunk(InternalFileId internalFileId, long totalDiskFileLength)
        {
            var chunkComplete = _fileRepFactory.ChunkComplete(internalFileId);
            chunkComplete.Delete();
            _fileRepFactory.ChunkStartPosition(internalFileId).Write(totalDiskFileLength.ToString());

            return chunkComplete;
        }

        private static void MarkChunkComplete(InternalFileRep chunkComplete)
        {
            chunkComplete.Write("1");
        }
    }
}