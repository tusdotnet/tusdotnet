using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Coordinates ongoing upload requests for a specific upload id.
    /// </summary>
    public interface IOngoingUploadManager
    {
        /// <summary>
        /// Preempts previous requests and acquires access for the current request.
        /// </summary>
        /// <param name="uploadId">The upload id to coordinate access for.</param>
        /// <returns>An active upload handle for the current request.</returns>
        Task<IOngoingUpload> AcquireAsync(string uploadId);

        /// <summary>
        /// Releases access for a previously acquired handle.
        /// </summary>
        /// <param name="upload">The upload handle to release.</param>
        Task ReleaseAsync(IOngoingUpload upload);
    }
}
