using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Store support for creation: http://tus.io/protocols/resumable-upload.html#creation
    /// </summary>
    public interface ITusCreationStore
    {
        /// <summary>
        /// Create a file upload reference that can later be used to upload data.
        /// </summary>
        /// <param name="uploadLength">The length of the upload in bytes</param>
        /// <param name="metadata">The Upload-Metadata request header or null if no header was provided</param>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>The id of the newly created file</returns>
        Task<string> CreateFileAsync(
            long uploadLength,
            string metadata,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Get the Upload-Metadata header as it was provided to <see cref="CreateFileAsync"/>.
        /// </summary>
        /// <param name="fileId">The id of the file to get the header for</param>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>The Upload-Metadata header</returns>
        Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken);
    }
}
