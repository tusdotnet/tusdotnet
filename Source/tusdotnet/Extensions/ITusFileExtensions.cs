using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="ITusFile"/>
    /// </summary>
    public static class ITusFileExtensions
    {
        /// <summary>
        /// Check if the <see cref="ITusFile"/> has been completely uploaded.
        /// </summary>
        /// <param name="file">The file to check</param>
        /// <param name="store">The store responsible for handling the file</param>
        /// <param name="cancellationToken">The cancellation token to use when cancelling</param>
        /// <returns>True if the file is completed, otherwise false</returns>
        public static async Task<bool> IsCompleteAsync(
            this ITusFile file,
            ITusStore store,
            CancellationToken cancellationToken
        )
        {
            var length = store.GetUploadLengthAsync(file.Id, cancellationToken);
            var offset = store.GetUploadOffsetAsync(file.Id, cancellationToken);

            await Task.WhenAll(length, offset);

            return length.Result == offset.Result;
        }
    }
}
