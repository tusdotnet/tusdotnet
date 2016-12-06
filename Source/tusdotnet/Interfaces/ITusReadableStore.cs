using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
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