using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Interfaces
{
	/// <summary>
	/// Store support for the core tus protocol.
	/// </summary>
	public interface ITusStore
	{
		/// <summary>
		/// Write data to the file using the provided stream.
		/// The implementation must throw <exception cref="TusStoreException"></exception> 
		/// if the streams length exceeds the upload length of the file.
		/// </summary>
		/// <param name="fileId">The id of the file to write</param>
		/// <param name="stream">The request input stream from the client</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The number of bytes written</returns>
		Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken);

		/// <summary>
		/// Check if a file exist.
		/// </summary>
		/// <param name="fileId">The id of the file to check</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>True if the file exists otherwise false</returns>
		Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken);

		/// <summary>
		/// Returns the upload length specified when the file was created or null if Defer-Upload-Length was used.
		/// </summary>
		/// <param name="fileId">The id of the file to check</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The upload length of the file</returns>
		Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken);

		/// <summary>
		/// Returns the current size of the file a.k.a. the upload offset.
		/// </summary>
		/// <param name="fileId">The id of the file to check</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The size of the current file</returns>
		Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken);
	}
}