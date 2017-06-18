using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Store support for upload-defer-length: http://tus.io/protocols/resumable-upload.html#upload-defer-length
    /// </summary>
    public interface ITusCreationDeferLengthStore
    {
        /// <summary>
        /// Set the upload length (in bytes) of the provided file.
        /// </summary>
        /// <param name="fileId">The id of the file to set the upload length for</param>
        /// <param name="uploadLength">The length of the upload in bytes</param>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>Task</returns>
        Task SetUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken);
    }
}
