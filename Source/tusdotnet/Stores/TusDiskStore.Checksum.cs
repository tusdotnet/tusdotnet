using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Extensions;
using tusdotnet.Helpers;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        private static readonly IEnumerable<string> _supportedAlgorithms = ["sha1"];

        /// <inheritdoc />
        public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken _)
        {
            return Task.FromResult(_supportedAlgorithms);
        }

        /// <inheritdoc />
        public async Task<bool> VerifyChecksumAsync(
            string fileId,
            string algorithm,
            byte[] checksum,
            CancellationToken _
        )
        {
            var valid = false;
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);

            using var dataStream = _fileRepFactory
                .Data(internalFileId)
                .GetStream(FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

            var chunkStartPosition = await _fileRepFactory
                .ChunkStartPosition(internalFileId)
                .ReadTextAsLongAsync(true, 0, CancellationToken.None);

            var chunkCompleteFile = _fileRepFactory.ChunkComplete(internalFileId);

            // If the client has provided a faulty checksum-trailer we should just discard the chunk.
            // Otherwise only verify the checksum if the entire lastest chunk has been written.
            // If not, just discard the last chunk as it won't match the checksum anyway.
            if (!ChecksumTrailerHelper.IsFallback(algorithm, checksum))
            {
                var calculatedChecksum = await chunkCompleteFile.ReadBytesAsync(
                    fileIsOptional: true,
                    CancellationToken.None
                );

                // If file is null the chunk wasn't completed so there is no need to verify the checksum.
                if (calculatedChecksum is not null)
                {
                    // If we don't have the optimized checksum file calculate it from the file stream.
                    if (calculatedChecksum is { Length: 1 })
                    {
                        calculatedChecksum = dataStream.CalculateSha1(chunkStartPosition);
                    }

                    valid = checksum.SequenceEqual(calculatedChecksum);
                }
            }

            if (!valid)
            {
                dataStream.SetLength(chunkStartPosition);
            }

            return valid;
        }

        private async Task<InternalFileRep> InitializeChunkAndGetCompleteFile(
            InternalFileId internalFileId,
            long totalDiskFileLength
        )
        {
            var chunkComplete = _fileRepFactory.ChunkComplete(internalFileId);
            chunkComplete.Delete();

            await _fileRepFactory
                .ChunkStartPosition(internalFileId)
                .WriteAsync(totalDiskFileLength.ToString());

            return chunkComplete;
        }

        private static async Task MarkChunkComplete(InternalFileRep chunkComplete, byte[] checksum)
        {
            await chunkComplete.WriteAsync(checksum ?? DefaultValueForChunkComplete);
        }

        // The string "1" due to backwards compatibility. Keep as static byte[] to not reallocate.
        private static readonly byte[] DefaultValueForChunkComplete = [49];
    }
}
