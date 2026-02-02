using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Stores
{
    /// <summary>
    /// Implements ITusStore - the core interface for file operations.
    /// </summary>
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task<bool> FileExistAsync(string fileId, CancellationToken _)
        {
            return _fileRepFactory
                .Data(await InternalFileId.Parse(_fileIdProvider, fileId))
                .Exist();
        }

        /// <inheritdoc />
        public async Task<long?> GetUploadLengthAsync(
            string fileId,
            CancellationToken cancellationToken
        )
        {
            var value = await _fileRepFactory
                .UploadLength(await InternalFileId.Parse(_fileIdProvider, fileId))
                .ReadTextAsLongAsync(true, long.MinValue, cancellationToken);

            return value == long.MinValue ? null : value;
        }

        /// <inheritdoc />
        public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken _)
        {
            return _fileRepFactory
                .Data(await InternalFileId.Parse(_fileIdProvider, fileId))
                .GetLength();
        }
    }
}
