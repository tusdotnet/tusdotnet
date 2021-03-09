using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Stores.BufferHandlers
{
    public class NonConsecutiveMemoryBufferHandler : IBufferHandler
    {
        public static NonConsecutiveMemoryBufferHandler Instance { get; } = new(useAsynchronousFileIO: true, useAsynchronousCodeFlow: true);

        // 84 KB. The limit to put items into the LOH is 85 KB.
        private const int LARGE_OBJECT_HEAP_LIMIT_IN_BYTES = 86_016;

        // TODO: Add support for customization
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
        private readonly bool _useAsynchronousFileIO;
        private readonly bool _useAsynchronousCodeFlow;

        public NonConsecutiveMemoryBufferHandler(bool useAsynchronousFileIO, bool useAsynchronousCodeFlow)
        {
            _useAsynchronousFileIO = useAsynchronousFileIO;
            _useAsynchronousCodeFlow = useAsynchronousCodeFlow;
        }

        public async Task<BufferHandlerCopyResult> CopyToFile(TusDiskBufferSize bufferSize, long fileUploadLengthProvidedDuringCreate, Stream stream, string fileLocation, CancellationToken cancellationToken)
        {
            var readSize = bufferSize.ReadBufferSizeInBytes;

            if (readSize > LARGE_OBJECT_HEAP_LIMIT_IN_BYTES)
            {
                readSize = LARGE_OBJECT_HEAP_LIMIT_IN_BYTES;
            }

            var fileOptions = FileOptions.SequentialScan;
            if (_useAsynchronousFileIO)
            {
                fileOptions |= FileOptions.Asynchronous;
            }
            using var fileStream = new FileStream(fileLocation, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, fileOptions);
            var writeBuffer = new NonconsecutiveMemoryWriteBuffer(_useAsynchronousCodeFlow, bufferSize.WriteBufferSizeInBytes, LARGE_OBJECT_HEAP_LIMIT_IN_BYTES, _bufferPool, fileStream);

            byte[] readBuffer = null;

            try
            {
                readBuffer = _bufferPool.Rent(readSize);

                int numberOfbytesReadFromClient;
                var bytesWrittenThisRequest = 0L;

                var totalDiskFileLength = fileStream.Length;

                bool clientDisconnectedDuringRead;
                do
                {
                    numberOfbytesReadFromClient = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);

                    clientDisconnectedDuringRead = cancellationToken.IsCancellationRequested;

                    totalDiskFileLength += numberOfbytesReadFromClient;

                    if (totalDiskFileLength > fileUploadLengthProvidedDuringCreate)
                    {
                        throw new TusStoreException($"Stream contains more data than the file's upload length. Stream data: {totalDiskFileLength}, upload length: {fileUploadLengthProvidedDuringCreate}.");
                    }

                    await writeBuffer.Append(readBuffer, numberOfbytesReadFromClient);

                    bytesWrittenThisRequest += numberOfbytesReadFromClient;
                }
                while (numberOfbytesReadFromClient != 0);

                await writeBuffer.FlushRemaining();

                return new BufferHandlerCopyResult(bytesWrittenThisRequest, clientDisconnectedDuringRead);
            }
            finally
            {
                if (readBuffer != null)
                {
                    _bufferPool.Return(readBuffer);
                }
            }
        }
    }
}
