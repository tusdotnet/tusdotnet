using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
	public class TusDiskFile : ITusFile
	{
		private readonly string _metadata;
		private readonly string _filePath;

		internal TusDiskFile(string directoryPath, string fileId, string metadata)
		{
			_metadata = metadata;
			Id = fileId;
			_filePath = Path.Combine(directoryPath, Id);
		}

		internal bool Exist()
		{
			return File.Exists(_filePath);
		}

		public string Id { get; }
		public Task<Stream> GetContentAsync(CancellationToken cancellationToken)
		{
			var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			return Task.FromResult<Stream>(stream);
		}

		public Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(Metadata.Parse(_metadata));
		}
	}
}
