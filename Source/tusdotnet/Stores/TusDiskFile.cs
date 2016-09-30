using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Stores
{
	public class TusDiskFile : ITusFile
	{
		private readonly string _filePath;

		internal TusDiskFile(string directoryPath, string fileId)
		{
			Id = fileId;
			_filePath = Path.Combine(directoryPath, Id);
		}

		internal bool Exist()
		{
			return File.Exists(_filePath);
		}

		public string Id { get; }
		public Task<Stream> GetContent(CancellationToken cancellationToken)
		{
			var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			return Task.FromResult<Stream>(stream);
		}
	}
}
