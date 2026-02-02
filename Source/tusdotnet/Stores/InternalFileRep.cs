using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Stores
{
    internal sealed class InternalFileRep
    {
        public string Path { get; }

        public string FileId { get; set; }

        private InternalFileRep(string fileId, string path)
        {
            FileId = fileId;
            Path = path;
        }

        public void Delete()
        {
            File.Delete(Path);
        }

        public bool Exist()
        {
            return File.Exists(Path);
        }

        public Task WriteAsync(string text)
        {
#if NETCOREAPP3_0_OR_GREATER
            return File.WriteAllTextAsync(Path, text);
#else
            File.WriteAllText(Path, text);
            return Task.FromResult(0);
#endif
        }

        public Task WriteAsync(byte[] data)
        {
#if NETCOREAPP3_0_OR_GREATER
            return File.WriteAllBytesAsync(Path, data);
#else
            File.WriteAllBytes(Path, data);
            return Task.FromResult(0);
#endif
        }

        public async Task<long> ReadTextAsLongAsync(
            bool fileIsOptional = false,
            long defaultValue = -1,
            CancellationToken cancellationToken = default
        )
        {
            var content = await ReadTextAsync(fileIsOptional, cancellationToken);
            if (long.TryParse(content, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public async Task<byte[]> ReadBytesAsync(
            bool fileIsOptional = false,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
#if NETCOREAPP3_0_OR_GREATER
                return await File.ReadAllBytesAsync(Path, cancellationToken);
#else
                return File.ReadAllBytes(Path);
#endif
            }
            catch (FileNotFoundException) when (fileIsOptional)
            {
                return null;
            }
        }

        public async Task<string> ReadTextAsync(
            bool fileIsOptional = false,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
#if NETCOREAPP3_0_OR_GREATER
                return await File.ReadAllTextAsync(Path, cancellationToken);
#else
                return File.ReadAllText(Path);
#endif
            }
            catch (FileNotFoundException) when (fileIsOptional)
            {
                return null;
            }
        }

        public FileStream GetStream(
            FileMode mode,
            FileAccess access,
            FileShare share,
            int bufferSize = 4096
        )
        {
            return new FileStream(
                Path,
                mode,
                access,
                share,
                bufferSize,
                FileOptions.SequentialScan | FileOptions.Asynchronous
            );
        }

        public long GetLength()
        {
            return new FileInfo(Path).Length;
        }

        internal sealed class FileRepFactory
        {
            private readonly string _directoryPath;

            public FileRepFactory(string directoryPath)
            {
                _directoryPath = directoryPath;
            }

            public InternalFileRep Data(InternalFileId fileId) => Create(fileId, "");

            public InternalFileRep UploadLength(InternalFileId fileId) =>
                Create(fileId, "uploadlength");

            public InternalFileRep UploadConcat(InternalFileId fileId) =>
                Create(fileId, "uploadconcat");

            public InternalFileRep Metadata(InternalFileId fileId) => Create(fileId, "metadata");

            public InternalFileRep Expiration(InternalFileId fileId) =>
                Create(fileId, "expiration");

            public InternalFileRep ChunkStartPosition(InternalFileId fileId) =>
                Create(fileId, "chunkstart");

            public InternalFileRep ChunkComplete(InternalFileId fileId) =>
                Create(fileId, "chunkcomplete");

            private InternalFileRep Create(InternalFileId fileId, string extension)
            {
                string fileName = fileId;
                if (!string.IsNullOrEmpty(extension))
                {
                    fileName += "." + extension;
                }

                return new InternalFileRep(
                    fileId,
                    System.IO.Path.Combine(_directoryPath, fileName)
                );
            }
        }
    }
}
