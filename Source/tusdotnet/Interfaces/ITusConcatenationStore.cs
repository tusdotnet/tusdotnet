using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Interfaces
{
	/// <summary>
	/// Store support for concatenation: http://tus.io/protocols/resumable-upload.html#concatenation
	/// </summary>
	public interface ITusConcatenationStore
	{
		/// <summary>
		/// Returns the type of Upload-Concat header that was used when creating the file.
		/// Returns null if no Upload-Concat was used.
		/// </summary>
		/// <param name="fileId">The file to check</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>FileConcatPartial, FileConcatFinal or null</returns>
		Task<FileConcat> GetUploadConcatAsync(string fileId, CancellationToken cancellationToken);

		/// <summary>
		/// Create a partial file. This method is called when a Upload-Concat header is present and when its value is "partial".
		/// </summary>
		/// <param name="uploadLength">The length of the upload in bytes</param>
		/// <param name="metadata">The Upload-Metadata request header or null if no header was provided</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The id of the newly created file</returns>
		Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken);

		/// <summary>
		/// Creates a final file by concatenating multiple files together. This method is called when a Upload-Concat header
		/// is present with a "final" value.
		/// </summary>
		/// <param name="partialFiles">List of file ids to concatenate</param>
		/// <param name="metadata">The Upload-Metadata request header or null if no header was provided</param>
		/// <param name="cancellationToken">Cancellation token to use when cancelling</param>
		/// <returns>The id of the newly created file</returns>
		Task<string> CreateFinalFileAsync(string[] partialFiles, string metadata, CancellationToken cancellationToken);
	}
}
