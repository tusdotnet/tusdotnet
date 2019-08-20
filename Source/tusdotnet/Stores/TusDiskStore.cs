using System;
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

namespace tusdotnet.Stores
{
    /// <summary>
    /// The built in data store that save files on disk.
    /// </summary>
    public class TusDiskStore: TusDiskStore<InternalFileId>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// Using this overload will not delete partial files if a final concatenation is performed.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        public TusDiskStore(string directoryPath) : base(directoryPath)
        {
          // Left blank.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat) : base(directoryPath, deletePartialFilesOnConcat)
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
            :base(directoryPath, deletePartialFilesOnConcat, bufferSize)
        {
        }
    }

    /// <summary>
    /// The built in data store that save files on disk with fileId provider TFileIdProvider.
    /// </summary>
    public class TusDiskStore<TFileIdProvider> :
        ITusStore,
        ITusCreationStore,
        ITusReadableStore,
        ITusTerminationStore,
        ITusChecksumStore,
        ITusConcatenationStore,
        ITusExpirationStore,
        ITusCreationDeferLengthStore
        where TFileIdProvider: ITusFileIdProvider, new()
    {
        private readonly string _directoryPath;
        private readonly bool _deletePartialFilesOnConcat;
        private readonly InternalFileRep.FileRepFactory _fileRepFactory;

        // These are the read and write buffers, they will get the value of TusDiskBufferSize.Default if not set in the constructor.
        private readonly int _maxReadBufferSize;
        private readonly int _maxWriteBufferSize;

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
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat) : this(directoryPath, deletePartialFilesOnConcat, TusDiskBufferSize.Default)
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
        {
            _directoryPath = directoryPath;
            _deletePartialFilesOnConcat = deletePartialFilesOnConcat;
            _fileRepFactory = new InternalFileRep.FileRepFactory(_directoryPath);

            if (bufferSize == null)
                bufferSize = TusDiskBufferSize.Default;

            _maxWriteBufferSize = bufferSize.WriteBufferSizeInBytes;
            _maxReadBufferSize = bufferSize.ReadBufferSizeInBytes;
        }

        /// <inheritdoc />
        public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
        {
            var internalFileId = new TFileIdProvider().Use(fileId);
            var reciveReadBuffer = new byte[_maxReadBufferSize];

            long bytesWritten = 0;
            var uploadLength = await GetUploadLengthAsync(fileId, cancellationToken);

            using (var writeBuffer = new MemoryStream(_maxWriteBufferSize))
            using (var file = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Append, FileAccess.Write, FileShare.None))
            {
                var fileLength = file.Length;
                if (uploadLength == fileLength)
                {
                    return 0;
                }

                var chunkComplete = _fileRepFactory.ChunkComplete(internalFileId);
                chunkComplete.Delete();

                _fileRepFactory.ChunkStartPosition(internalFileId).Write(fileLength.ToString());

                int bytesRead;
                var clientDisconnectedDuringRead = false;
                do
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    bytesRead = await stream.ReadAsync(reciveReadBuffer, 0, _maxReadBufferSize, cancellationToken);
                    clientDisconnectedDuringRead = cancellationToken.IsCancellationRequested;

                    fileLength += bytesRead;

                    if (fileLength > uploadLength)
                    {
                        throw new TusStoreException(
                            $"Stream contains more data than the file's upload length. Stream data: {fileLength}, upload length: {uploadLength}.");
                    }

                    writeBuffer.Write(reciveReadBuffer, 0, bytesRead);
                    bytesWritten += bytesRead;

                    // If the buffer is above max size we flush it now.
                    if (writeBuffer.Length >= _maxWriteBufferSize)
                        await FlushFileWriteBuffer(writeBuffer, file);

                } while (bytesRead != 0);

                // Flush the remaining buffer to disk.
                if (writeBuffer.Length != 0)
                    await FlushFileWriteBuffer(writeBuffer, file);

                if (!clientDisconnectedDuringRead)
                {
                    // Chunk is complete. Mark it as complete.
                    chunkComplete.Write("1");
                }

                return bytesWritten;
            }
        }

        /// <inheritdoc />
        public Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_fileRepFactory.Data(new TFileIdProvider().Use(fileId)).Exist());
        }

        /// <inheritdoc />
        public Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
        {
            var firstLine = _fileRepFactory.UploadLength(new TFileIdProvider().Use(fileId)).ReadFirstLine(true);
            return Task.FromResult(firstLine == null
                ? (long?)null
                : long.Parse(firstLine));
        }

        /// <inheritdoc />
        public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_fileRepFactory.Data(new TFileIdProvider().Use(fileId)).GetLength());
        }

        /// <inheritdoc />
        public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var fileId = new TFileIdProvider();
            File.Create(_fileRepFactory.Data(fileId).Path).Dispose();
            if (uploadLength != -1)
            {
                await SetUploadLengthAsync(fileId.FileId, uploadLength, cancellationToken);
            }
            _fileRepFactory.Metadata(fileId).Write(metadata);
            return fileId.FileId;
        }

        /// <inheritdoc />
        public Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
        {
            var firstLine = _fileRepFactory.Metadata(new TFileIdProvider().Use(fileId)).ReadFirstLine(true);
            return string.IsNullOrEmpty(firstLine) ? Task.FromResult<string>(null) : Task.FromResult(firstLine);
        }

        /// <inheritdoc />
        public Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
        {
            var internalFileId = new TFileIdProvider().Use(fileId);
            var data = _fileRepFactory.Data(internalFileId);

            return Task.FromResult<ITusFile>(data.Exist()
                ? new TusDiskFile(data, _fileRepFactory.Metadata(internalFileId))
                : null);
        }

        /// <inheritdoc />
        public Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
        {
            var internalFileId = new TFileIdProvider().Use(fileId);
            return Task.Run(() =>
            {
                _fileRepFactory.Data(internalFileId).Delete();
                _fileRepFactory.UploadLength(internalFileId).Delete();
                _fileRepFactory.Metadata(internalFileId).Delete();
                _fileRepFactory.UploadConcat(internalFileId).Delete();
                _fileRepFactory.Expiration(internalFileId).Delete();
                _fileRepFactory.ChunkStartPosition(internalFileId).Delete();
                _fileRepFactory.ChunkComplete(internalFileId).Delete();
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<string>>(new[] { "sha1" });
        }

        /// <inheritdoc />
        public Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)
        {
            var valid = false;
            var internalFileId = new TFileIdProvider().Use(fileId);
            using (var dataStream = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                var chunkPositionFile = _fileRepFactory.ChunkStartPosition(internalFileId);
                var chunkStartPosition = chunkPositionFile.ReadFirstLineAsLong(true, 0);
                var chunkCompleteFile = _fileRepFactory.ChunkComplete(internalFileId);

                // Only verify the checksum if the entire lastest chunk has been written.
                // If not, just discard the last chunk as it won't match the checksum anyway.
                if (chunkCompleteFile.Exist())
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

            return Task.FromResult(valid);
        }

        /// <inheritdoc />
        public Task<FileConcat> GetUploadConcatAsync(string fileId, CancellationToken cancellationToken)
        {
            var firstLine = _fileRepFactory.UploadConcat(new TFileIdProvider().Use(fileId)).ReadFirstLine(true);
            return Task.FromResult(string.IsNullOrWhiteSpace(firstLine)
                ? null
                : new UploadConcat(firstLine).Type);
        }

        /// <inheritdoc />
        public async Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var fileId = await CreateFileAsync(uploadLength, metadata, cancellationToken);
            _fileRepFactory.UploadConcat(new TFileIdProvider().Use(fileId)).Write(new FileConcatPartial().GetHeader());
            return fileId;
        }

        /// <inheritdoc />
        public async Task<string> CreateFinalFileAsync(string[] partialFiles, string metadata, CancellationToken cancellationToken)
        {
            var partialInternalFileReps = partialFiles.Select(f =>
            {
                var partialData = _fileRepFactory.Data(new TFileIdProvider().Use(f));

                if (!partialData.Exist())
                {
                    throw new TusStoreException($"File {f} does not exist");
                }

                return partialData;
            }).ToArray();

            var length = partialInternalFileReps.Sum(f => f.GetLength());

            var fileId = await CreateFileAsync(length, metadata, cancellationToken);

            var internalFileId = new TFileIdProvider().Use(fileId);

            _fileRepFactory.UploadConcat(internalFileId).Write(new FileConcatFinal(partialFiles).GetHeader());

            using (var finalFile = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Open, FileAccess.Write, FileShare.None))
            {
                foreach (var partialFile in partialInternalFileReps)
                {
                    using (var partialStream = partialFile.GetStream(FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        partialStream.CopyTo(finalFile);
                    }
                }
            }

            // ReSharper disable once InvertIf
            if (_deletePartialFilesOnConcat)
            {
                await Task.WhenAll(partialInternalFileReps.Select(f => DeleteFileAsync(f.FileId, cancellationToken)));
            }

            return fileId;
        }

        /// <inheritdoc />
        public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        {
            _fileRepFactory.Expiration(new TFileIdProvider().Use(fileId)).Write(expires.ToString("O"));
            return TaskHelper.Completed;
        }

        /// <inheritdoc />
        public Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
        {
            var expiration = _fileRepFactory.Expiration(new TFileIdProvider().Use(fileId)).ReadFirstLine(true);

            return Task.FromResult(expiration == null
                ? (DateTimeOffset?)null
                : DateTimeOffset.ParseExact(expiration, "O", null));
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
        {
            var expiredFiles = Directory.EnumerateFiles(_directoryPath, "*.expiration")
                .Select(Path.GetFileNameWithoutExtension)
                .Select(f => new TFileIdProvider().Use(f))
                .Where(f => FileHasExpired(f) && FileIsIncomplete(f))
                .Select(f => f.FileId)
                .ToList();

            return Task.FromResult<IEnumerable<string>>(expiredFiles);

            bool FileHasExpired(ITusFileIdProvider fileId)
            {
                var firstLine = _fileRepFactory.Expiration(fileId).ReadFirstLine();
                return !string.IsNullOrWhiteSpace(firstLine)
                       && DateTimeOffset.ParseExact(firstLine, "O", null).HasPassed();
            }

            bool FileIsIncomplete(ITusFileIdProvider fileId)
            {
                return _fileRepFactory.UploadLength(fileId).ReadFirstLineAsLong()
                        != _fileRepFactory.Data(fileId).GetLength();
            }
        }

        /// <inheritdoc />
        public async Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
        {
            return await Cleanup(await GetExpiredFilesAsync(cancellationToken));

            async Task<int> Cleanup(IEnumerable<string> files)
            {
                var tasks = files.Select(file => DeleteFileAsync(file, cancellationToken)).ToList();
                await Task.WhenAll(tasks);
                return tasks.Count;
            }
        }

        /// <inheritdoc />
        public Task SetUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
        {
            _fileRepFactory.UploadLength(new TFileIdProvider().Use(fileId)).Write(uploadLength.ToString());
            return TaskHelper.Completed;
        }

        private async Task FlushFileWriteBuffer(MemoryStream buffer, FileStream fileStream)
        {
            buffer.WriteTo(fileStream);

            await fileStream.FlushAsync();

            buffer.SetLength(0); // Best way to clear a memory buffer.
        }
    }
}