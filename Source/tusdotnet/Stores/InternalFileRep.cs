using System.IO;

namespace tusdotnet.Stores
{
    internal sealed class InternalFileRep
    {
        private string Path { get; }

        private InternalFileRep(string path)
        {
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

        private string ReadFirstLine(bool fileIsOptional = false)
        {
            if (fileIsOptional && !File.Exists(Path))
            {
                return null;
            }

            using (var stream = File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sr = new StreamReader(stream))
                {
                    return sr.ReadLine();
                }
            }
        }

        internal sealed class FileRepFactory
        {
            private readonly string _directoryPath;

            public FileRepFactory(string directoryPath)
            {
                _directoryPath = directoryPath;
            }

            public InternalFileRep ChunkStartPosition(string fileId) => Create($"{fileId}.chunkstart");

            public InternalFileRep ChunkComplete(string fileId) => Create($"{fileId}.chunkcomplete");

            private InternalFileRep Create(string fileName)
            {
                return new InternalFileRep(System.IO.Path.Combine(_directoryPath, fileName));
            }
        }
    }
}
