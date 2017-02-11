using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	/// <summary>
	/// Support for reading from the store. This interface does not represent anything from the tus standard
	/// but is rather a complemenet to make it easier to work with files in tusdotnet.
	/// </summary>
	public interface ITusReadableStore
	{
		/// <summary>
		/// Get the file with the specified id. 
		/// Returns null if the file was not found.
		/// </summary>
		/// <param name="fileId">The id of the file to get</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The file or null if the file was not found</returns>
		Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken);
	}
}