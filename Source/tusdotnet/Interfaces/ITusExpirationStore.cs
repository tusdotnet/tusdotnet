using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Store support for expiration: http://tus.io/protocols/resumable-upload.html#expiration
    /// </summary>
    public interface ITusExpirationStore
    {
        /// <summary>
        /// Set the expiry date of the provided file.
        /// This method will be called once during creation if absolute expiration is used.
        /// This method will be called once per patch request if sliding expiration is used.
        /// </summary>
        /// <param name="fileId">The id of the file to update the expiry date for</param>
        /// <param name="expires">The datetime offset when the file expires</param>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>Task</returns>
        Task SetExpirationAsync(
            string fileId,
            DateTimeOffset expires,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Get the expiry date of the provided file (set by <see cref="SetExpirationAsync" />).
        /// If the datetime offset returned has passed an error will be returned to the client.
        /// If no expiry date exist for the file, this method returns null.
        /// </summary>
        /// <param name="fileId">The id of the file to get the expiry date for</param>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns></returns>
        Task<DateTimeOffset?> GetExpirationAsync(
            string fileId,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Returns a list of ids of incomplete files that have expired.
        /// This method can be used to do batch processing of incomplete, expired files before removing them.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>A list of ids of incomplete files that have expired</returns>
        Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Remove all incomplete files that have expired.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>The number of files that were removed</returns>
        Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken);
    }
}
