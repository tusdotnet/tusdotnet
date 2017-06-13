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
    public class TusDiskStore :
        ITusStore,
        ITusCreationStore,
        ITusReadableStore,
        ITusTerminationStore,
        ITusChecksumStore,
        ITusConcatenationStore,
        ITusExpirationStore
    {
        private readonly string _directoryPath;
        private readonly Dictionary<string, long> _lengthBeforeWrite;
        private readonly bool _deletePartialFilesOnConcat;

        // Number of bytes to read at the time from the input stream.
        // The lower the value, the less data needs to be re-submitted on errors.
        // However, the lower the value, the slower the operation is. 51200 = 50 KB.
        private const int ByteChunkSize = 5120000;

        public TusDiskStore(string directoryPath) : this(directoryPath, false)
        {
            // Left blank.
        }

        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat)
        {
            _directoryPath = directoryPath;
            _lengthBeforeWrite = new Dictionary<string, long>();
            _deletePartialFilesOnConcat = deletePartialFilesOnConcat;
        }

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

        public Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(File.Exists(GetPath(fileId)));
        }

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

        public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileInfo(GetPath(fileId)).Length);
        }

        public Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var fileId = Guid.NewGuid().ToString("n");
            var path = GetPath(fileId);
            File.Create(path).Dispose();
            File.WriteAllText($"{path}.uploadlength", uploadLength.ToString());
            File.WriteAllText($"{path}.metadata", metadata);
            return Task.FromResult(fileId);
        }

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
                var path = GetPath(fileId);
                File.Delete(path);
                File.Delete($"{path}.uploadlength");
                File.Delete($"{path}.metadata");
                File.Delete($"{path}.uploadconcat");
                File.Delete($"{path}.expiration");
            }, cancellationToken);
        }

        public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new[] { "sha1" } as IEnumerable<string>);
        }

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


        public async Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var fileId = await CreateFileAsync(uploadLength, metadata, cancellationToken);
            File.WriteAllText($"{GetPath(fileId)}.uploadconcat", new FileConcatPartial().GetHeader());
            return fileId;
        }

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

        public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
                {
                    File.WriteAllText($"{GetPath(fileId)}.expiration", expires.ToString("O"));
                },
                cancellationToken);
        }

        public Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
        {
            var expiration = ReadFirstLine($"{GetPath(fileId)}.expiration");
            return Task.FromResult(expiration == null
                ? (DateTimeOffset?) null
                : DateTimeOffset.ParseExact(expiration, "O", null));
        }

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

        private string GetPath(string fileId)
        {
            return Path.Combine(_directoryPath, fileId);
        }

        private static string ReadFirstLine(string filePath)
        {
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