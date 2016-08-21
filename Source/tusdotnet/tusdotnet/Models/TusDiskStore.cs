using System;
using System.IO;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.Models
{
	// TODO: Error handling
	public class TusDiskStore : ITusStore, ITusCreationStore
	{
		private readonly string _directoryPath;

		public TusDiskStore(string directoryPath)
		{
			_directoryPath = directoryPath;
		}

		public Task AppendDataAsync(string fileName, byte[] data)
		{
			var path = Path.Combine(_directoryPath, fileName);
			using (var stream = File.Open(path, FileMode.Append, FileAccess.Write))
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

			var path = Path.Combine(_directoryPath, fileName) + ".uploadlength";

			if (!File.Exists(path))
			{
				return Task.FromResult<long?>(null);
			}

			var res = long.Parse(File.ReadAllLines(path)[0]);
			return Task.FromResult(new long?(res));
		}

		public Task<long> GetUploadOffsetAsync(string fileName)
		{
			return Task.FromResult(new FileInfo(Path.Combine(_directoryPath, fileName)).Length);
		}

		public Task<string> CreateFileAsync(long? uploadLength)
		{
			var fileName = Guid.NewGuid().ToString("n");
			var path = Path.Combine(_directoryPath, fileName);
			File.Create(path).Dispose();

			if (uploadLength != null)
			{
				var uploadLengthFile = path + ".uploadlength";
				File.WriteAllText(uploadLengthFile, uploadLength.ToString());
			}

			return Task.FromResult(fileName);
		}
	}
}