using System.IO;
#if NETCOREAPP2_0_OR_GREATER
using System.IO.Abstractions;
#endif

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
        
#if NETCOREAPP2_0_OR_GREATER
        private readonly IFileSystem _fileSystem;

        private InternalFileRep(string fileId, string path, IFileSystem fileSystem)
        {
            FileId = fileId;
            Path = path;
            _fileSystem = fileSystem;
        }
#endif

        public void Delete()
        {
#if NETCOREAPP2_0_OR_GREATER
            _fileSystem.File.Delete(Path);
#else
            File.Delete(Path);
#endif
        }

        public bool Exist()
        {
#if NETCOREAPP2_0_OR_GREATER
            return _fileSystem.File.Exists(Path);
#else
            return File.Exists(Path);
#endif
        }

        public void Write(string text)
        {
#if NETCOREAPP2_0_OR_GREATER
            _fileSystem.File.WriteAllText(Path, text);
#else
            File.WriteAllText(Path, text);
#endif
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
#if NETCOREAPP2_0_OR_GREATER
            var fileExists =  _fileSystem.File.Exists(Path);
#else
            var fileExists =  File.Exists(Path);
#endif           
            
            if (fileIsOptional && !fileExists)
            {
                return null;
            }

            using var stream = GetStream(FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(stream);
            return sr.ReadLine();
            
        }

        public FileStream GetStream(FileMode mode, FileAccess access, FileShare share, int bufferSize = 4096)
        {
#if NETCOREAPP2_0_OR_GREATER
            return (FileStream)_fileSystem.FileStream.New(Path, mode, access, share, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
#else
            return new FileStream(Path, mode, access, share, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
#endif  
        }

        public long GetLength()
        {
#if NETCOREAPP2_0_OR_GREATER
            return _fileSystem.FileInfo.New(Path).Length;
#else
            return new FileInfo(Path).Length;
#endif  
        }

        internal sealed class FileRepFactory
        {
            private readonly string _directoryPath;

            public FileRepFactory(string directoryPath)
            {
                _directoryPath = directoryPath;
            }
            
#if NETCOREAPP2_0_OR_GREATER
            private readonly IFileSystem _fileSystem;

            public FileRepFactory(string directoryPath, IFileSystem fileSystem)
            {
                _directoryPath = directoryPath;
                _fileSystem = fileSystem;
            }
#endif 

            public InternalFileRep Data(InternalFileId fileId) => Create(fileId, "");

            public InternalFileRep UploadLength(InternalFileId fileId) => Create(fileId, "uploadlength");

            public InternalFileRep UploadConcat(InternalFileId fileId) => Create(fileId, "uploadconcat");

            public InternalFileRep Metadata(InternalFileId fileId) => Create(fileId, "metadata");

            public InternalFileRep Expiration(InternalFileId fileId) => Create(fileId, "expiration");

            public InternalFileRep ChunkStartPosition(InternalFileId fileId) => Create(fileId, "chunkstart");

            public InternalFileRep ChunkComplete(InternalFileId fileId) => Create(fileId, "chunkcomplete");

            private InternalFileRep Create(InternalFileId fileId, string extension)
            {
                string fileName = fileId;
                if (!string.IsNullOrEmpty(extension))
                {
                    fileName += "." + extension;
                }

#if NETCOREAPP2_0_OR_GREATER
                return new InternalFileRep(fileId, _fileSystem.Path.Combine(_directoryPath, fileName), _fileSystem);
#else
                return new InternalFileRep(fileId, System.IO.Path.Combine(_directoryPath, fileName));
#endif 
            }
        }
    }
}
