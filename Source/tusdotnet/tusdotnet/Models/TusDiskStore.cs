using System.IO;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Models
{
	public class TusDiskStore : ITusStore
	{
		private readonly string _directoryPath;

		public TusDiskStore(string directoryPath)
		{
			_directoryPath = directoryPath;
		}

		// TODO: Error handling
		public Task AppendDataAsync(string fileName, byte[] data)
		{
			var path = Path.Combine(_directoryPath, fileName);
			using (var stream = File.Open(fileName, FileMode.Append, FileAccess.Write))
			{
				stream.Write(data, 0, data.Length);
			}

			return Task.FromResult(0);
		}

		public Task<bool> FileExistAsync(string fileName)
		{
			return Task.FromResult(File.Exists(Path.Combine(_directoryPath, fileName)));
		}

		public Task<long?> GetUploadLengthAsync(string fileName)
		{
			// TODO: Implement, currently not supported.
			return Task.FromResult<long?>(null);
		}

		public Task<long> GetUploadOffsetAsync(string fileName)
		{
			return Task.FromResult(new FileInfo(Path.Combine(_directoryPath, fileName)).Length);
		}
	}
}