using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Stores
{
    /// <summary>
    /// ITusReadableStore implementation
    /// </summary>
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task<ITusFile> GetFileAsync(string fileId, CancellationToken _)
        {
            var internalFileId = await InternalFileId.Parse(_fileIdProvider, fileId);
            var data = _fileRepFactory.Data(internalFileId);

            return data.Exist()
                ? new TusDiskFile(data, _fileRepFactory.Metadata(internalFileId))
                : null;
        }
    }
}
