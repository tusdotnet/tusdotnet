using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Extensions.Store;
using tusdotnet.Models;
using tusdotnet.Stores.Hashers;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task<long> AppendDataAsync(
            string fileId,
            Stream stream,
            CancellationToken cancellationToken
        )
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            var httpReadBuffer = _bufferPool.Rent(_maxReadBufferSize);
            var fileWriteBuffer = _bufferPool.Rent(
                Math.Max(_maxWriteBufferSize, _maxReadBufferSize)
            );

            try
            {
                using var diskFileStream = await TryOpenStreamDueToNetFxAndNetworkShareIssue(
                    internalFileId,
                    cancellationToken
                );

                if (diskFileStream is null)
                {
                    return 0;
                }

                var fileUploadLengthProvidedDuringCreate = await GetUploadLengthAsync(
                    fileId,
                    cancellationToken
                );

                var totalDiskFileLength = diskFileStream.Length;
                if (fileUploadLengthProvidedDuringCreate == totalDiskFileLength)
                {
                    return 0;
                }

                var checksumInfo = stream.GetUploadChecksumInfo();

                using var hasher = TusDiskStoreHasher.Create(checksumInfo?.Algorithm);

                var chunkCompleteFile = await InitializeChunkAndGetCompleteFile(
                    internalFileId,
                    totalDiskFileLength
                );

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

                    numberOfbytesReadFromClient = await stream.ReadAsync(
                        httpReadBuffer,
                        0,
                        _maxReadBufferSize,
                        cancellationToken
                    );

                    clientDisconnectedDuringRead = cancellationToken.IsCancellationRequested;

                    totalDiskFileLength += numberOfbytesReadFromClient;

                    if (totalDiskFileLength > fileUploadLengthProvidedDuringCreate)
                    {
                        throw new TusStoreException(
                            $"Stream contains more data than the file's upload length. Stream data: {totalDiskFileLength}, upload length: {fileUploadLengthProvidedDuringCreate}."
                        );
                    }

                    // Can we fit the read data into the write buffer? If not flush it now.
                    if (
                        writeBufferNextFreeIndex + numberOfbytesReadFromClient
                        > _maxWriteBufferSize
                    )
                    {
                        await diskFileStream.WriteAsync(
                            fileWriteBuffer,
                            0,
                            writeBufferNextFreeIndex,
                            CancellationToken.None
                        );

                        hasher.Append(fileWriteBuffer, writeBufferNextFreeIndex);

                        writeBufferNextFreeIndex = 0;
                    }

                    Array.Copy(
                        sourceArray: httpReadBuffer,
                        sourceIndex: 0,
                        destinationArray: fileWriteBuffer,
                        destinationIndex: writeBufferNextFreeIndex,
                        length: numberOfbytesReadFromClient
                    );

                    writeBufferNextFreeIndex += numberOfbytesReadFromClient;
                    bytesWrittenThisRequest += numberOfbytesReadFromClient;
                } while (numberOfbytesReadFromClient != 0);

                // Write the remaining buffer to disk.
                if (writeBufferNextFreeIndex != 0)
                {
                    await diskFileStream.WriteAsync(
                        fileWriteBuffer,
                        0,
                        writeBufferNextFreeIndex,
                        CancellationToken.None
                    );
                    hasher.Append(fileWriteBuffer, writeBufferNextFreeIndex);
                }

                await diskFileStream.FlushAsync(CancellationToken.None);

                if (!clientDisconnectedDuringRead)
                {
                    await MarkChunkComplete(chunkCompleteFile, hasher.GetHashAndReset());
                }

                return bytesWrittenThisRequest;
            }
            finally
            {
                _bufferPool.Return(httpReadBuffer);
                _bufferPool.Return(fileWriteBuffer);
            }
        }

#if netfull

        private async Task<FileStream> TryOpenStreamDueToNetFxAndNetworkShareIssue(
            InternalFileId internalFileId,
            CancellationToken cancellationToken
        )
        {
            // Work around for issue described in this PR: https://github.com/tusdotnet/tusdotnet/pull/228

            const int MAX_ATTEMPTS = 10;
            const int RETRY_DELAY_MS = 1_000;

            Exception openException = null;
            FileStream diskFileStream = null;

            for (int i = 0; i < MAX_ATTEMPTS; i++)
            {
                try
                {
                    diskFileStream = _fileRepFactory
                        .Data(internalFileId)
                        .GetStream(FileMode.Append, FileAccess.Write, FileShare.None);

                    break;
                }
                catch (Exception e)
                {
                    openException = e;
                    try
                    {
                        await Task.Delay(RETRY_DELAY_MS, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                }
            }

            if (diskFileStream is null)
            {
                throw openException;
            }

            return diskFileStream;
        }

#else

        private Task<FileStream> TryOpenStreamDueToNetFxAndNetworkShareIssue(
            InternalFileId internalFileId,
            CancellationToken _
        )
        {
            return Task.FromResult(
                _fileRepFactory
                    .Data(internalFileId)
                    .GetStream(FileMode.Append, FileAccess.Write, FileShare.None)
            );
        }

#endif
    }
}
