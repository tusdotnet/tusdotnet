using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Stores
{
	// TODO: Enable async operations: 
	// https://msdn.microsoft.com/en-us/library/mt674879.aspx
	/// <summary>
	/// The built in data store that save files on disk.
	/// </summary>
	public class TusDiskStore :
		ITusStore,
		ITusCreationStore,
		ITusReadableStore,
		ITusTerminationStore,
		ITusChecksumStore,
		ITusConcatenationStore,
		ITusExpirationStore,
		ITusCreationDeferLengthStore
	{
		private readonly string _directoryPath;
		private readonly Dictionary<string, long> _lengthBeforeWrite;
		private readonly bool _deletePartialFilesOnConcat;

		// Number of bytes to read at the time from the input stream.
		// The lower the value, the less data needs to be re-submitted on errors.
		// However, the lower the value, the slower the operation is. 51200 = 50 KB.
		private const int ByteChunkSize = 5120000;

		/// <summary>
		/// Initializes a new instance of the <see cref="TusDiskStore"/> class.
		/// Using this overload will not delete partial files if a final concatenation is performed.
		/// </summary>
		/// <param name="directoryPath">The path on disk where to save files</param>
		public TusDiskStore(string directoryPath) : this(directoryPath, false)
		{
			// Left blank.
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TusDiskStore"/> class.
		/// </summary>
		/// <param name="directoryPath">The path on disk where to save files</param>
		/// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
		public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat)
		{
			_directoryPath = directoryPath;
			_lengthBeforeWrite = new Dictionary<string, long>();
			_deletePartialFilesOnConcat = deletePartialFilesOnConcat;
		}

		/// <inheritdoc />
		public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
		{
			var path = GetPath(fileId);
			long bytesWritten = 0;
			var uploadLength = await GetUploadLengthAsync(fileId, cancellationToken);
			using (var file = File.Open(path, FileMode.Append, FileAccess.Write))
			{
				var fileLength = file.Length;
				if (uploadLength == fileLength)
				{
					return 0;
				}

				_lengthBeforeWrite[fileId] = fileLength;

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

		/// <inheritdoc />
		public Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
		{
			return Task.FromResult(File.Exists(GetPath(fileId)));
		}

		/// <inheritdoc />
		public Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
		{
			var path = GetPath(fileId) + ".uploadlength";

			if (!File.Exists(path))
			{
				return Task.FromResult<long?>(null);
			}

			var firstLine = ReadFirstLine(path);

			return firstLine == null
				? Task.FromResult<long?>(null)
				: Task.FromResult(new long?(long.Parse(firstLine)));
		}

		/// <inheritdoc />
		public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
		{
			return Task.FromResult(new FileInfo(GetPath(fileId)).Length);
		}

		/// <inheritdoc />
		public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
		{
			var fileId = Guid.NewGuid().ToString("n");
			var path = GetPath(fileId);
			File.Create(path).Dispose();
			if (uploadLength != -1)
			{
				await SetUploadLengthAsync(fileId, uploadLength, cancellationToken);
			}
			File.WriteAllText($"{path}.metadata", metadata);
			return fileId;
		}

		/// <inheritdoc />
		public Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
		{
			var path = GetPath(fileId) + ".metadata";

			if (!File.Exists(path))
			{
				return Task.FromResult<string>(null);
			}

			var firstLine = ReadFirstLine(path);
			return string.IsNullOrEmpty(firstLine) ? Task.FromResult<string>(null) : Task.FromResult(firstLine);
		}

		/// <inheritdoc />
		public async Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
		{
			var metadata = await GetUploadMetadataAsync(fileId, cancellationToken);
			var file = new TusDiskFile(_directoryPath, fileId, metadata);
			return (file.Exist() ? file : null);
		}

		/// <inheritdoc />
		public Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
		{
			return Task.Run(() =>
			{
				var path = GetPath(fileId);
				File.Delete(path);
				File.Delete($"{path}.uploadlength");
				File.Delete($"{path}.metadata");
				File.Delete($"{path}.uploadconcat");
				File.Delete($"{path}.expiration");
			}, cancellationToken);
		}

		/// <inheritdoc />
		public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(new[] { "sha1" } as IEnumerable<string>);
		}

		/// <inheritdoc />
		public Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)
		{
			bool valid;
			using (var stream = new FileStream(GetPath(fileId), FileMode.Open, FileAccess.ReadWrite))
			{
				valid = checksum.SequenceEqual(stream.CalculateSha1());

				// ReSharper disable once InvertIf
				if (!valid && _lengthBeforeWrite.ContainsKey(fileId))
				{
					stream.Seek(0, SeekOrigin.Begin);
					stream.SetLength(_lengthBeforeWrite[fileId]);
					_lengthBeforeWrite.Remove(fileId);
				}
			}

			return Task.FromResult(valid);
		}

		/// <inheritdoc />
		public Task<FileConcat> GetUploadConcatAsync(string fileId, CancellationToken cancellationToken)
		{
			var uploadconcat = $"{GetPath(fileId)}.uploadconcat";
			if (!File.Exists(uploadconcat))
			{
				return Task.FromResult<FileConcat>(null);
			}

			var firstLine = ReadFirstLine(uploadconcat);
			return string.IsNullOrWhiteSpace(firstLine)
				? Task.FromResult<FileConcat>(null)
				: Task.FromResult(new UploadConcat(firstLine).Type);
		}

		/// <inheritdoc />
		public async Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
		{
			var fileId = await CreateFileAsync(uploadLength, metadata, cancellationToken);
			File.WriteAllText($"{GetPath(fileId)}.uploadconcat", new FileConcatPartial().GetHeader());
			return fileId;
		}

		/// <inheritdoc />
		public async Task<string> CreateFinalFileAsync(string[] partialFiles, string metadata, CancellationToken cancellationToken)
		{
			var fileInfos = partialFiles.Select(f =>
			{
				var fi = new FileInfo(GetPath(f));
				if (!fi.Exists)
				{
					throw new TusStoreException($"File {f} does not exist");
				}
				return fi;
			}).ToArray();

			var length = fileInfos.Sum(f => f.Length);

			var fileId = await CreateFileAsync(length, metadata, cancellationToken);

			var path = GetPath(fileId);
			File.WriteAllText(
				$"{path}.uploadconcat",
				new FileConcatFinal(partialFiles).GetHeader()
			);

			using (var finalFile = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
			{
				foreach (var partialFile in fileInfos)
				{
					using (var partialStream = partialFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						partialStream.CopyTo(finalFile);
					}
				}
			}

			// ReSharper disable once InvertIf
			if (_deletePartialFilesOnConcat)
			{
				foreach (var partialFile in partialFiles)
				{
					File.Delete(GetPath(partialFile));
				}
			}

			return fileId;
		}

		/// <inheritdoc />
		public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
		{
			return Task.Run(() =>
				{
					File.WriteAllText($"{GetPath(fileId)}.expiration", expires.ToString("O"));
				},
				cancellationToken);
		}

		/// <inheritdoc />
		public Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
		{
			var expiration = ReadFirstLine($"{GetPath(fileId)}.expiration", true);
			return Task.FromResult(expiration == null
				? (DateTimeOffset?)null
				: DateTimeOffset.ParseExact(expiration, "O", null));
		}

		/// <inheritdoc />
		public Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
		{
			var expiredFiles = Directory.EnumerateFiles(_directoryPath, "*.expiration")
				.Where(FileHasExpired)
				.Where(FileIsIncomplete)
				.Select(FileId)
				.ToList();

			return Task.FromResult<IEnumerable<string>>(expiredFiles);

			bool FileHasExpired(string filePath)
			{
				var firstLine = ReadFirstLine(filePath);
				return !string.IsNullOrWhiteSpace(firstLine) &&
					   DateTimeOffset.ParseExact(firstLine, "O", null).HasPassed();
			}

			bool FileIsIncomplete(string filePath)
			{
				var file = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
				return ReadFirstLine($"{file}.uploadlength") != new FileInfo(file).Length.ToString();
			}

			string FileId(string filePath) => Path.GetFileNameWithoutExtension(filePath);
		}

		/// <inheritdoc />
		public async Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
		{
			return await Cleanup(await GetExpiredFilesAsync(cancellationToken));

			async Task<int> Cleanup(IEnumerable<string> files)
			{
				var tasks = files.Select(file => DeleteFileAsync(file, cancellationToken)).ToList();
				await Task.WhenAll(tasks);
				return tasks.Count;
			}
		}

		/// <inheritdoc />
		public Task SetUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
		{
			return Task.Run(() =>
			{
				var path = GetPath(fileId);
				File.WriteAllText($"{path}.uploadlength", uploadLength.ToString());
			}, cancellationToken);
		}

		private string GetPath(string fileId)
		{
			return Path.Combine(_directoryPath, fileId);
		}

		/// <summary>
		/// Read the first line of the file specified.
		/// </summary>
		/// <param name="filePath">The path to read</param>
		/// <param name="fileIsOptional">If true and the file does not exist, null will be returned. Otherwise an exception will be thrown</param>
		/// <returns>The first line of the file specified</returns>
		private static string ReadFirstLine(string filePath, bool fileIsOptional = false)
		{
			if (fileIsOptional && !File.Exists(filePath))
			{
				return null;
			}

			using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var sr = new StreamReader(stream))
				{
					return sr.ReadLine();
				}
			}
		}
	}
}