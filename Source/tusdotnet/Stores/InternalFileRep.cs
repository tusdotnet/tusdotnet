using System.IO;
using tusdotnet.Interfaces;

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

        public void Write(string text)
        {
            File.WriteAllText(Path, text);
        }

        public long ReadFirstLineAsLong(bool fileIsOptional = false, long defaultValue = -1)
        {
            var content = ReadFirstLine(fileIsOptional);
            if (long.TryParse(content, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public string ReadFirstLine(bool fileIsOptional = false)
        {
            if (fileIsOptional && !File.Exists(Path))
            {
                return null;
            }

            using (var stream = GetStream(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sr = new StreamReader(stream))
                {
                    return sr.ReadLine();
                }
            }
        }

        public FileStream GetStream(FileMode mode, FileAccess access, FileShare share)
        {
            return File.Open(Path, mode, access, share);
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

            public InternalFileRep Data(ITusFileIdProvider fileId) => Create(fileId, "");

            public InternalFileRep UploadLength(ITusFileIdProvider fileId) => Create(fileId, "uploadlength");

            public InternalFileRep UploadConcat(ITusFileIdProvider fileId) => Create(fileId, "uploadconcat");

            public InternalFileRep Metadata(ITusFileIdProvider fileId) => Create(fileId, "metadata");

            public InternalFileRep Expiration(ITusFileIdProvider fileId) => Create(fileId, "expiration");

            public InternalFileRep ChunkStartPosition(ITusFileIdProvider fileId) => Create(fileId, "chunkstart");

            public InternalFileRep ChunkComplete(ITusFileIdProvider fileId) => Create(fileId, "chunkcomplete");

            private InternalFileRep Create(ITusFileIdProvider fileId, string extension)
            {
                var fileName = fileId.FileId;
                if (!string.IsNullOrEmpty(extension))
                {
                    fileName += "." + extension;
                }

                return new InternalFileRep(fileId.FileId, System.IO.Path.Combine(_directoryPath, fileName));
            }
        }
    }
}
