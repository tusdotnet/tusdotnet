using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task<string> CreateFileAsync(
            long uploadLength,
            string metadata,
            CancellationToken _
        )
        {
            var fileId = await InternalFileId.CreateNew(_fileIdProvider, metadata);
            new FileStream(_fileRepFactory.Data(fileId).Path, FileMode.CreateNew).Dispose();
            if (uploadLength != -1)
            {
                await SetUploadLengthAsync(fileId, uploadLength, CancellationToken.None);
            }
            await _fileRepFactory.Metadata(fileId).WriteAsync(metadata);
            return fileId;
        }

        /// <inheritdoc />
        public async Task<string> GetUploadMetadataAsync(
            string fileId,
            CancellationToken cancellationToken
        )
        {
            var firstLine = await _fileRepFactory
                .Metadata(await InternalFileId.Parse(_fileIdProvider, fileId))
                .ReadTextAsync(true, cancellationToken);
            return string.IsNullOrEmpty(firstLine) ? null : firstLine;
        }
    }
}
