#if NETCOREAPP2_0_OR_GREATER
using System.IO.Abstractions;
using tusdotnet.Interfaces;
using tusdotnet.Stores.FileIdProviders;

namespace tusdotnet.Stores
{
    public partial class TusDiskStore
    {
        private readonly IFileSystem _fileSystem;
        private static readonly IFileSystem _defaultFileSystem = new FileSystem();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        /// <param name="bufferSize">The buffer size to use when reading and writing. If unsure use <see cref="TusDiskBufferSize.Default"/>.</param>
        /// <param name="fileIdProvider">The provider that generates ids for files. If unsure use <see cref="GuidFileIdProvider"/>.</param>
        /// <param name="fileSystem">The abstraction of the file system.</param>
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat, TusDiskBufferSize bufferSize, ITusFileIdProvider fileIdProvider, IFileSystem fileSystem)
        {
            _directoryPath = directoryPath;
            _deletePartialFilesOnConcat = deletePartialFilesOnConcat;
            _fileRepFactory = new InternalFileRep.FileRepFactory(_directoryPath, fileSystem);

            if (bufferSize == null)
                bufferSize = TusDiskBufferSize.Default;

            _maxWriteBufferSize = bufferSize.WriteBufferSizeInBytes;
            _maxReadBufferSize = bufferSize.ReadBufferSizeInBytes;

            _fileIdProvider = fileIdProvider;

            _fileSystem = fileSystem;
        }
    }
}
#endif