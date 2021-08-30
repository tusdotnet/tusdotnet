#if pipelines

using System;
using System.IO.Pipelines;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using tusdotnet.Extensions;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task<long> AppendDataAsync(string fileId, PipeReader reader, CancellationToken cancellationToken)
        {
            const int JUST_BELOW_LOH_BYTE_LIMIT = 84 * 1024;

            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            var fileUploadLengthProvidedDuringCreate = await GetUploadLengthAsync(fileId, cancellationToken);
            using var diskFileStream = _fileRepFactory.Data(internalFileId).GetStream(FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: JUST_BELOW_LOH_BYTE_LIMIT);

            var fileSizeOnDisk = diskFileStream.Length;
            if (fileUploadLengthProvidedDuringCreate == fileSizeOnDisk)
            {
                return 0;
            }

            var chunkCompleteFile = InitializeChunk(internalFileId, fileSizeOnDisk);

            var bytesWrittenThisRequest = 0L;
            ReadResult result = default;
            bool latestDataHasBeenFlushedToDisk = false;

            try
            {
                while (!PipeReadingIsDone(result, cancellationToken))
                {
                    result = await reader.ReadAsync(cancellationToken);

                    AssertNotToMuchData(fileSizeOnDisk, result.Buffer.Length, fileUploadLengthProvidedDuringCreate);

                    if (result.Buffer.Length >= _maxWriteBufferSize)
                    {
                        await diskFileStream.FlushToDisk(result.Buffer);

                        bytesWrittenThisRequest += result.Buffer.Length;
                        fileSizeOnDisk += result.Buffer.Length;

                        reader.AdvanceTo(result.Buffer.End);

                        // Flag that the buffer has been written so that we do not accidentally
                        // write the data twice if no more data exist or if the data does not cross the write buffer size.
                        latestDataHasBeenFlushedToDisk = true;
                    }
                    else
                    {
                        reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
                        latestDataHasBeenFlushedToDisk = false;
                    }
                }

                if (!latestDataHasBeenFlushedToDisk && result.Buffer.Length > 0)
                {
                    AssertNotToMuchData(fileSizeOnDisk, result.Buffer.Length, fileUploadLengthProvidedDuringCreate);

                    bytesWrittenThisRequest += result.Buffer.Length;
                    await diskFileStream.FlushToDisk(result.Buffer);
                }

                await reader.CompleteAsync();
            }
            catch (Exception)
            {
                // Clear memory and complete the reader to not cause a Microsoft.AspNetCore.Connections.ConnectionAbortedException inside Kestrel later on as this is an "expected" exception.
                reader.AdvanceTo(result.Buffer.End);
                await reader.CompleteAsync();
                throw;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                MarkChunkComplete(chunkCompleteFile);
            }

            return bytesWrittenThisRequest;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PipeReadingIsDone(ReadResult result, CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested || result.IsCanceled || result.IsCompleted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertNotToMuchData(long originalDiskFileLength, long numberOfBytesReadFromClient, long? fileUploadLengthProvidedDuringCreate)
        {
            var newDiskFileLength = originalDiskFileLength + numberOfBytesReadFromClient;

            if (newDiskFileLength > fileUploadLengthProvidedDuringCreate)
            {
                throw new TusStoreException($"Request contains more data than the file's upload length. Request data: {newDiskFileLength}, upload length: {fileUploadLengthProvidedDuringCreate}.");
            }
        }
    }
}

#endif
