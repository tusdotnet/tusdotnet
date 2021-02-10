using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Stores.FileIdProviders;

#if NETCOREAPP3_0
using System.IO.Pipelines;
#endif

namespace tusdotnet.Stores
{
    /// <summary>
    /// The built in data store that save files on disk.
    /// </summary>
    public class TusDiskStore :
        ITusStore,
#if NETCOREAPP3_0
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

        // These are the read and write buffers, they will get the value of TusDiskBufferSize.Default if not set in the constructor.
        private readonly int _maxReadBufferSize;
        private readonly int _maxWriteBufferSize;

        // Use our own array pool to not leak data to other parts of the running app.
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create();

        private static readonly TusGuidProvider DefaultFileIdProvider = new TusGuidProvider();

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
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat, TusDiskBufferSize bufferSize) : this(directoryPath, deletePartialFilesOnConcat, bufferSize, DefaultFileIdProvider)
        {
            // Left blank.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        /// <param name="bufferSize">The buffer size to use when reading and writing. If unsure use <see cref="TusDiskBufferSize.Default"/>.</param>
        /// <param name="fileIdProvider">The provider that generates ids for files. If unsure use <see cref="TusGuidProvider"/>.</param>
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

#if NETCOREAPP3_0
        /// <inheritdoc />
        public async Task<long> AppendDataAsync(string fileId, PipeReader pipeReader, CancellationToken cancellationToken)
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            //var fileWriteBuffer = _bufferPool.Rent(Math.Max(_maxWriteBufferSize, _maxReadBufferSize));

            var fileUploadLengthProvidedDuringCreate = await GetUploadLengthAsync(fileId, cancellationToken).ConfigureAwait(false);
            using var diskFileStream = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Append, FileAccess.Write, FileShare.None, true, _maxWriteBufferSize);

            var totalDiskFileLength = diskFileStream.Length;
            if (fileUploadLengthProvidedDuringCreate == totalDiskFileLength)
            {
                return 0;
            }

            var chunkCompleteFile = InitializeChunk(internalFileId, totalDiskFileLength);

            var bytesWrittenThisRequest = 0L;
            var clientDisconnectedDuringRead = false;
            //var writeBufferNextFreeIndex = 0;

            while (true)
            {
                ReadResult result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition position = buffer.Start;
                SequencePosition consumed = position;

                try
                {
                    if (result.IsCanceled)
                    {
                        clientDisconnectedDuringRead = true;
                        break;
                    }

                    if (totalDiskFileLength + buffer.Length > fileUploadLengthProvidedDuringCreate)
                    {
                        throw new TusStoreException($"Stream contains more data than the file's upload length. Stream data: {totalDiskFileLength + buffer.Length}, upload length: {fileUploadLengthProvidedDuringCreate}.");
                    }

                    // Direct copy
                    while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory))
                    {
                        await diskFileStream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);

                        bytesWrittenThisRequest += memory.Length;
                        totalDiskFileLength += memory.Length;
                        consumed = position;
                    }

                    // Buffered copy
                    /*while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory))
                    {
                        if (writeBufferNextFreeIndex + memory.Length > fileWriteBuffer.Length)
                        {
                            await diskFileStream.WriteAsync(fileWriteBuffer, 0, writeBufferNextFreeIndex);
                            writeBufferNextFreeIndex = 0;
                        }

                        memory.CopyTo(fileWriteBuffer.AsMemory().Slice(writeBufferNextFreeIndex));

                        writeBufferNextFreeIndex += memory.Length;
                        bytesWrittenThisRequest += memory.Length;
                        totalDiskFileLength += memory.Length;
                        consumed = position;
                    }*/

                    // The while loop completed succesfully, so we've consumed the entire buffer.
                    consumed = buffer.End;

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                finally
                {
                    // Always advance so the PipeReader is not left in the
                    // currently reading state
                    pipeReader.AdvanceTo(consumed);
                }
            }

            // Flush the remaining buffer to disk.
            //if (writeBufferNextFreeIndex != 0)
            //    await FlushFileToDisk(fileWriteBuffer, diskFileStream, writeBufferNextFreeIndex);
            await diskFileStream.FlushAsync();

            if (!clientDisconnectedDuringRead)
            {
                MarkChunkComplete(chunkCompleteFile);
            }

            return bytesWrittenThisRequest;

        }
#endif

        /// <inheritdoc />
        public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            var httpReadBuffer = _bufferPool.Rent(_maxReadBufferSize);
            var fileWriteBuffer = _bufferPool.Rent(Math.Max(_maxWriteBufferSize, _maxReadBufferSize));

            try
            {
                var fileUploadLengthProvidedDuringCreate = await GetUploadLengthAsync(fileId, cancellationToken);
                using var diskFileStream = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Append, FileAccess.Write, FileShare.None);

                var totalDiskFileLength = diskFileStream.Length;
                if (fileUploadLengthProvidedDuringCreate == totalDiskFileLength)
                {
                    return 0;
                }

                var chunkCompleteFile = InitializeChunk(internalFileId, totalDiskFileLength);

                int numberOfbytesReadFromClient;
                var bytesWrittenThisRequest = 0L;
                var clientDisconnectedDuringRead = false;
                var writeBufferNextFreeIndex = 0;

                do
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    numberOfbytesReadFromClient = await stream.ReadAsync(httpReadBuffer, 0, _maxReadBufferSize, cancellationToken);
                    clientDisconnectedDuringRead = cancellationToken.IsCancellationRequested;

                    totalDiskFileLength += numberOfbytesReadFromClient;

                    if (totalDiskFileLength > fileUploadLengthProvidedDuringCreate)
                    {
                        throw new TusStoreException($"Stream contains more data than the file's upload length. Stream data: {totalDiskFileLength}, upload length: {fileUploadLengthProvidedDuringCreate}.");
                    }

                    // Can we fit the read data into the write buffer? If not flush it now.
                    if (writeBufferNextFreeIndex + numberOfbytesReadFromClient > _maxWriteBufferSize)
                    {
                        await FlushFileToDisk(fileWriteBuffer, diskFileStream, writeBufferNextFreeIndex);
                        writeBufferNextFreeIndex = 0;
                    }

                    Array.Copy(
                        sourceArray: httpReadBuffer,
                        sourceIndex: 0,
                        destinationArray: fileWriteBuffer,
                        destinationIndex: writeBufferNextFreeIndex,
                        length: numberOfbytesReadFromClient);

                    writeBufferNextFreeIndex += numberOfbytesReadFromClient;
                    bytesWrittenThisRequest += numberOfbytesReadFromClient;

                } while (numberOfbytesReadFromClient != 0);

                // Flush the remaining buffer to disk.
                if (writeBufferNextFreeIndex != 0)
                    await FlushFileToDisk(fileWriteBuffer, diskFileStream, writeBufferNextFreeIndex);

                if (!clientDisconnectedDuringRead)
                {
                    MarkChunkComplete(chunkCompleteFile);
                }

                return bytesWrittenThisRequest;
            }
            finally
            {
                _bufferPool.Return(httpReadBuffer);
                _bufferPool.Return(fileWriteBuffer);
            }
        }

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
            new FileStream(_fileRepFactory.Data(fileId).Path, FileMode.CreateNew).Dispose();
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
            return Task.FromResult<IEnumerable<string>>(new[] { "sha1" });
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
                ? (DateTimeOffset?)null
                : DateTimeOffset.ParseExact(expiration, "O", null);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken _)
        {
            List<string> expiredFiles = new List<string>();
            foreach (var file in Directory.EnumerateFiles(_directoryPath, "*.expiration"))
            {
                var f = await InternalFileId.Parse(_fileIdProvider, Path.GetFileNameWithoutExtension(file));
                if (FileHasExpired(f) && FileIsIncomplete(f))
                {
                    expiredFiles.Add(f);
                }
            }

            return expiredFiles;

            bool FileHasExpired(InternalFileId fileId)
            {
                var firstLine = _fileRepFactory.Expiration(fileId).ReadFirstLine();
                return !string.IsNullOrWhiteSpace(firstLine)
                       && DateTimeOffset.ParseExact(firstLine, "O", null).HasPassed();
            }

            bool FileIsIncomplete(InternalFileId fileId)
            {
                var uploadLength = _fileRepFactory.UploadLength(fileId).ReadFirstLineAsLong(fileIsOptional: true, defaultValue: long.MinValue);

                if (uploadLength == long.MinValue)
                {
                    return true;
                }

                var dataFile = _fileRepFactory.Data(fileId);

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

        private void MarkChunkComplete(InternalFileRep chunkComplete)
        {
            chunkComplete.Write("1");
        }

        private static async Task FlushFileToDisk(byte[] fileWriteBuffer, FileStream fileStream, int writeBufferNextFreeIndex)
        {
            await fileStream.WriteAsync(fileWriteBuffer, 0, writeBufferNextFreeIndex);
            await fileStream.FlushAsync();
        }
    }
}