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

            var chunkStartPosition = _fileRepFactory
                .ChunkStartPosition(internalFileId)
                .ReadFirstLineAsLong(true, 0);

            var chunkCompleteFile = _fileRepFactory.ChunkComplete(internalFileId);

            // If the client has provided a faulty checksum-trailer we should just discard the chunk.
            // Otherwise only verify the checksum if the entire lastest chunk has been written.
            // If not, just discard the last chunk as it won't match the checksum anyway.
            if (!ChecksumTrailerHelper.IsFallback(algorithm, checksum) && chunkCompleteFile.Exist())
            {
                var calculatedChecksum = chunkCompleteFile.ReadBytes();

                // If we don't have the optimized checksum file calculate it from the file stream.
                if (calculatedChecksum is { Length: 1 })
                {
                    calculatedChecksum = dataStream.CalculateSha1(chunkStartPosition);
                }

                valid = checksum.SequenceEqual(calculatedChecksum);
            }

            if (!valid)
            {
                dataStream.SetLength(chunkStartPosition);
            }

            return valid;
        }

        private InternalFileRep InitializeChunk(
            InternalFileId internalFileId,
            long totalDiskFileLength
        )
        {
            var chunkComplete = _fileRepFactory.ChunkComplete(internalFileId);
            chunkComplete.Delete();

            _fileRepFactory
                .ChunkStartPosition(internalFileId)
                .Write(totalDiskFileLength.ToString());

            return chunkComplete;
        }

        private static void MarkChunkComplete(InternalFileRep chunkComplete, byte[] checksum)
        {
            chunkComplete.Write(checksum ?? DefaultValueForChunkComplete);
        }

        // The string "1" due to backwards compatibility. Keep as static byte[] to not reallocate.
        private static readonly byte[] DefaultValueForChunkComplete = [49];
    }
}
