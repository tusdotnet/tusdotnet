using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Extensions;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            var httpReadBuffer = _bufferPool.Rent(_maxReadBufferSize);
            var fileWriteBuffer = _bufferPool.Rent(Math.Max(_maxWriteBufferSize, _maxReadBufferSize));
            FileStream diskFileStream = null;

            try
            {
                var fileUploadLengthProvidedDuringCreate = await GetUploadLengthAsync(fileId, cancellationToken);

                Exception openException = null;
                for (int i = 0; i < 10; i++)
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
                        await Task.Delay(1000);
                    }
                }

                if (diskFileStream is null)
                    throw openException;

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
                        await diskFileStream.FlushFileToDisk(fileWriteBuffer, writeBufferNextFreeIndex);
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
                    await diskFileStream.FlushFileToDisk(fileWriteBuffer, writeBufferNextFreeIndex);

                if (!clientDisconnectedDuringRead)
                {
                    MarkChunkComplete(chunkCompleteFile);
                }

                return bytesWrittenThisRequest;
            }
            finally
            {
                diskFileStream?.Dispose();

                _bufferPool.Return(httpReadBuffer);
                _bufferPool.Return(fileWriteBuffer);
            }
        }
    }
}
