using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
	public interface ITusTerminationStore
	{
		/// <summary>
		/// Delete a file from the data store.
		/// </summary>
		/// <param name="fileId">The id of the file to delete</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>Task</returns>
		Task DeleteFileAsync(string fileId, CancellationToken cancellationToken);
	}
}
