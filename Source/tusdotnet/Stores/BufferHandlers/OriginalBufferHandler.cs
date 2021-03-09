using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Stores.BufferHandlers
{
    public class OriginalBufferHandler : IBufferHandler
    {
        // Use our own array pool to not leak data to other parts of the running app.
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create();

        public static OriginalBufferHandler Instance { get; } = new();

        public async Task<BufferHandlerCopyResult> CopyToFile(TusDiskBufferSize bufferSize, long fileUploadLengthProvidedDuringCreate, Stream stream, string fileLocation, CancellationToken cancellationToken)
        {
            int numberOfbytesReadFromClient;
            var bytesWrittenThisRequest = 0L;
            var clientDisconnectedDuringRead = false;
            var writeBufferNextFreeIndex = 0;

            var _maxReadBufferSize = bufferSize.ReadBufferSizeInBytes;
            var _maxWriteBufferSize = bufferSize.WriteBufferSizeInBytes;

            var httpReadBuffer = _bufferPool.Rent(_maxReadBufferSize);
            var fileWriteBuffer = _bufferPool.Rent(Math.Max(_maxWriteBufferSize, _maxReadBufferSize));

            try
            {
                using var diskFileStream = new FileStream(fileLocation, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                var totalDiskFileLength = diskFileStream.Length;

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
            }
            finally
            {
                _bufferPool.Return(httpReadBuffer);
                _bufferPool.Return(fileWriteBuffer);
            }

            return new BufferHandlerCopyResult(bytesWrittenThisRequest, clientDisconnectedDuringRead);
        }

        private static async Task FlushFileToDisk(byte[] fileWriteBuffer, FileStream fileStream, int writeBufferNextFreeIndex)
        {
            await fileStream.WriteAsync(fileWriteBuffer, 0, writeBufferNextFreeIndex);
            await fileStream.FlushAsync();
        }
    }
}
