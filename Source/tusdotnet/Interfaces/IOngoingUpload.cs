using System.Threading;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Represents an active ongoing upload request for a specific upload id.
    /// </summary>
    public interface IOngoingUpload
    {
        /// <summary>
        /// The upload id associated with this request.
        /// </summary>
        string UploadId { get; }

        /// <summary>
        /// Cancellation token that is cancelled when this request should stop.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// True if this request has been cancelled by a newer request.
        /// </summary>
        bool IsCancellationRequested { get; }
    }
}
