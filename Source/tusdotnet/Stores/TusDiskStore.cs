using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Stores.FileIdProviders;

namespace tusdotnet.Stores
{
    /// <summary>
    /// The built in data store that save files on disk.
    /// This partial class contains constructors and shared fields.
    /// Interface implementations are in separate partial files per interface.
    /// </summary>
    public partial class TusDiskStore
        : ITusStore,
#if pipelines
            ITusPipelineStore,
#endif
            ITusCreationStore,
            ITusReadableStore,
            ITusTerminationStore,
            ITusChecksumStore,
            ITusConcatenationStore,
            ITusExpirationStore,
            ITusCreationDeferLengthStore
    {
        private readonly string _directoryPath;
        private readonly bool _deletePartialFilesOnConcat;
        private readonly InternalFileRep.FileRepFactory _fileRepFactory;
        private readonly ITusFileIdProvider _fileIdProvider;

        // These are the read and write buffer sizes, they will get the value of TusDiskBufferSize.Default if not set in the constructor.
        private readonly int _maxReadBufferSize;
        private readonly int _maxWriteBufferSize;

        // Use our own array pool to not leak data to other parts of the running app.
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create();

        private static readonly GuidFileIdProvider _defaultFileIdProvider = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// Using this overload will not delete partial files if a final concatenation is performed.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        public TusDiskStore(string directoryPath)
            : this(directoryPath, false, TusDiskBufferSize.Default)
        {
            // Left blank.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        public TusDiskStore(string directoryPath, bool deletePartialFilesOnConcat)
            : this(directoryPath, deletePartialFilesOnConcat, TusDiskBufferSize.Default)
        {
            // Left blank.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        /// <param name="bufferSize">The buffer size to use when reading and writing. If unsure use <see cref="TusDiskBufferSize.Default"/>.</param>
        public TusDiskStore(
            string directoryPath,
            bool deletePartialFilesOnConcat,
            TusDiskBufferSize bufferSize
        )
            : this(directoryPath, deletePartialFilesOnConcat, bufferSize, _defaultFileIdProvider)
        {
            // Left blank.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskStore"/> class.
        /// </summary>
        /// <param name="directoryPath">The path on disk where to save files</param>
        /// <param name="deletePartialFilesOnConcat">True to delete partial files if a final concatenation is performed</param>
        /// <param name="bufferSize">The buffer size to use when reading and writing. If unsure use <see cref="TusDiskBufferSize.Default"/>.</param>
        /// <param name="fileIdProvider">The provider that generates ids for files. If unsure use <see cref="GuidFileIdProvider"/>.</param>
        public TusDiskStore(
            string directoryPath,
            bool deletePartialFilesOnConcat,
            TusDiskBufferSize bufferSize,
            ITusFileIdProvider fileIdProvider
        )
        {
            _directoryPath = directoryPath;
            _deletePartialFilesOnConcat = deletePartialFilesOnConcat;
            _fileRepFactory = new InternalFileRep.FileRepFactory(_directoryPath);

            bufferSize ??= TusDiskBufferSize.Default;

            _maxWriteBufferSize = bufferSize.WriteBufferSizeInBytes;
            _maxReadBufferSize = bufferSize.ReadBufferSizeInBytes;

            _fileIdProvider = fileIdProvider;
        }
    }
}
