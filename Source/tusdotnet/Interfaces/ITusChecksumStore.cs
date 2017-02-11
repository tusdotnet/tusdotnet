using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	/// <summary>
	/// Store support for checksum: http://tus.io/protocols/resumable-upload.html#checksum
	/// </summary>
	public interface ITusChecksumStore
	{
		/// <summary>
		/// Returns a collection of hash algorithms that the store supports (e.g. sha1).
		/// </summary>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The collection of hash algorithms</returns>
		Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Verify that the provided checksum matches the file checksum.
		/// </summary>
		/// <param name="fileId">The id of the file to check</param>
		/// <param name="algorithm">The checksum algorithm to use when checking. This algorithm must be supported by the store.</param>
		/// <param name="checksum">The checksom to use for verification</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>True if the checksum matches otherwise false</returns>
		Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken);
	}
}
