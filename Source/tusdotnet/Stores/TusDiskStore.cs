using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
	public class TusDiskStore : ITusStore, ITusCreationStore, ITusReadableStore, ITusTerminationStore
	{
		private readonly string _directoryPath;
		// Number of bytes to read at the time from the input stream.
		// The lower the value, the less data needs to be re-submitted on errors.
		// However, the lower the value, the slower the operation is. 51200 = 50 KB.
		private const int ByteChunkSize = 51200;

		public TusDiskStore(string directoryPath)
		{
			_directoryPath = directoryPath;
		}

		public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
		{
			var path = Path.Combine(_directoryPath, fileId);
			long bytesWritten = 0;
			var uploadLength = await GetUploadLengthAsync(fileId, cancellationToken);
			using (var file = File.Open(path, FileMode.Append, FileAccess.Write))
			{
				var fileLength = file.Length;
				if (uploadLength == fileLength)
				{
					return bytesWritten;
				}

				int bytesRead;
				do
				{
					if (cancellationToken.IsCancellationRequested)
					{
						break;
					}

					var buffer = new byte[ByteChunkSize];
					bytesRead = await stream.ReadAsync(buffer, 0, ByteChunkSize, cancellationToken);

					fileLength += bytesRead;

					if (fileLength > uploadLength)
					{
						throw new TusStoreException(
							$"Stream contains more data than the file's upload length. Stream data: {fileLength}, upload length: {uploadLength}.");
					}

					file.Write(buffer, 0, bytesRead);
					bytesWritten += bytesRead;

				} while (bytesRead != 0);

				return bytesWritten;
			}
		}

		public Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
		{
			return Task.FromResult(File.Exists(Path.Combine(_directoryPath, fileId)));
		}

		public Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
		{
			var path = Path.Combine(_directoryPath, fileId) + ".uploadlength";

			if (!File.Exists(path))
			{
				return Task.FromResult<long?>(null);
			}

			using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var sr = new StreamReader(stream))
				{
					var firstLine = sr.ReadLine();
					if (string.IsNullOrWhiteSpace(firstLine))
					{
						return Task.FromResult<long?>(null);
					}

					var res = long.Parse(firstLine);
					return Task.FromResult(new long?(res));
				}

			}
		}

		public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
		{
			return Task.FromResult(new FileInfo(Path.Combine(_directoryPath, fileId)).Length);
		}

		public Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
		{
			var fileName = Guid.NewGuid().ToString("n");
			var path = Path.Combine(_directoryPath, fileName);
			File.Create(path).Dispose();
			File.WriteAllText($"{path}.uploadlength", uploadLength.ToString());
			File.WriteAllText($"{path}.metadata", metadata);
			return Task.FromResult(fileName);
		}

		public Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
		{
			var path = Path.Combine(_directoryPath, fileId) + ".metadata";

			if (!File.Exists(path))
			{
				return Task.FromResult<string>(null);
			}

			using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var sr = new StreamReader(stream))
				{
					var firstLine = sr.ReadLine();
					return string.IsNullOrEmpty(firstLine) ? Task.FromResult<string>(null) : Task.FromResult(firstLine);
				}
			}
		}

		public async Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
		{
			var metadata = await GetUploadMetadataAsync(fileId, cancellationToken);
			var file = new TusDiskFile(_directoryPath, fileId, metadata);
			return (file.Exist() ? file : null);
		}

		public Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
		{
			return Task.Run(() =>
			{
				var path = Path.Combine(_directoryPath, fileId);
				File.Delete(path);
				File.Delete($"{path}.uploadlength");
				File.Delete($"{path}.metadata");
			}, cancellationToken);
		}
	}
}