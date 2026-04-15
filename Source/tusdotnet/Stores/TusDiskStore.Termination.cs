using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Stores
{
    /// <summary>
    /// ITusTerminationStore implementation
    /// </summary>
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task DeleteFileAsync(string fileId, CancellationToken _)
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);
            await Task.Run(
                () =>
                {
                    _fileRepFactory.Data(internalFileId).Delete();
                    _fileRepFactory.UploadLength(internalFileId).Delete();
                    _fileRepFactory.Metadata(internalFileId).Delete();
                    _fileRepFactory.UploadConcat(internalFileId).Delete();
                    _fileRepFactory.ChunkStartPosition(internalFileId).Delete();
                    _fileRepFactory.ChunkComplete(internalFileId).Delete();
                    _fileRepFactory.Expiration(internalFileId).Delete();
                },
                CancellationToken.None
            );
        }
    }
}
