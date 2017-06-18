using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Represents a file saved in the data store.
    /// </summary>
    public interface ITusFile
    {
        /// <summary>
        /// The id of the file.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Returns the content of the file as a stream.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>The file's content as a stream</returns>
        Task<Stream> GetContentAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns the file's metadata or an empty dictionary if no metadata exist.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to use when cancelling</param>
        /// <returns>The file's metadata</returns>
        Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken);
    }
}