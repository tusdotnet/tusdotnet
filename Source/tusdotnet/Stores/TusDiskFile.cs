using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
	/// <summary>
	/// Represents a file saved in the TusDiskStore data store
	/// </summary>
	public class TusDiskFile : ITusFile
	{
		private readonly string _metadata;
		private readonly string _filePath;

		/// <summary>
		/// Initializes a new instance of the <see cref="TusDiskFile"/> class.
		/// </summary>
		///  <param name="directoryPath">The directory path on disk where the store save it's files</param>
		/// <param name="fileId">The file id</param>
		/// <param name="metadata">The raw Upload-Metadata header</param>
		internal TusDiskFile(string directoryPath, string fileId, string metadata)
		{
			_metadata = metadata;
			Id = fileId;
			_filePath = Path.Combine(directoryPath, Id);
		}

		/// <summary>
		/// Returns true if the file exists.
		/// </summary>
		/// <returns>True if the file exists</returns>
		internal bool Exist()
		{
			return File.Exists(_filePath);
		}

		/// <inheritdoc />
		public string Id { get; }

		/// <inheritdoc />
		public Task<Stream> GetContentAsync(CancellationToken cancellationToken)
		{
			var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			return Task.FromResult<Stream>(stream);
		}

		/// <inheritdoc />
		public Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(Metadata.Parse(_metadata));
		}
	}
}
