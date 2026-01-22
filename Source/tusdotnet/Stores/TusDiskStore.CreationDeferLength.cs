using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        /// <inheritdoc />
        public async Task SetUploadLengthAsync(
            string fileId,
            long uploadLength,
            CancellationToken _
        )
        {
            _fileRepFactory
                .UploadLength(await InternalFileId.Parse(_fileIdProvider, fileId))
                .Write(uploadLength.ToString());
        }
    }
}
