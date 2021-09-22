using tusdotnet.Models;

namespace tusdotnet.Stores
{
    /// <summary>
    /// The buffer sizes for reads and writes to use with <see cref="TusDiskStore"/>
    /// </summary>
    public class TusDiskBufferSize
    {
        /// <summary>
        /// The default read buffer size used when nothing else is specified.
        /// Default value is 50 KB. 
        /// </summary>
        public const int DefaultReadBufferSizeInBytes = 51200;

        /// <summary>
        /// The default write buffer size used when nothing else is specified.
        /// Default value is 50 KB.
        /// </summary>
        public const int DefaultWriteBufferSizeInBytes = 51200;

        /// <summary>
        /// The default buffer size used when nothing else is specified.
        /// Uses <see cref="DefaultWriteBufferSizeInBytes"/> and <see cref="DefaultReadBufferSizeInBytes"/>.
        /// </summary>
        public static TusDiskBufferSize Default { get; } = new TusDiskBufferSize(DefaultWriteBufferSizeInBytes, DefaultWriteBufferSizeInBytes);

        /// <summary>
        /// The read buffer size to use for this instance.
        /// </summary>
        public int ReadBufferSizeInBytes { get; }

        /// <summary>
        /// The write buffer size to use for this instance.
        /// </summary>
        public int WriteBufferSizeInBytes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskBufferSize"/> class using the provided write buffer size and <see cref="DefaultReadBufferSizeInBytes"/>.
        /// </summary>
        /// <param name="writeBufferSizeInBytes">The amount of data to keep in memory before flushing data to disk. A higher value requires more RAM but uses less CPU and IO</param>
        public TusDiskBufferSize(int writeBufferSizeInBytes)
            : this(writeBufferSizeInBytes, DefaultReadBufferSizeInBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TusDiskBufferSize"/> class using the provided write and read buffer sizes.
        /// </summary>
        /// <param name="writeBufferSizeInBytes">The number of bytes to keep in memory before flushing data to disk. A higher value requires more RAM but uses less CPU and IO</param>
        /// <param name="readBufferSizeInBytes">The number of bytes to read at the time from the request. The lower the value, the less data needs to be re-submitted on errors. However, the lower the value, the slower the operation is. Please note that changing this value does nothing if using the request's PipeReader by setting <code>DefaultTusConfiguration.UsePipelinesIfAvailable = true</code> as it will always use the default PipeReader segment limit (4096 bytes)</param>
        public TusDiskBufferSize(int writeBufferSizeInBytes, int readBufferSizeInBytes)
        {
            WriteBufferSizeInBytes = AssertNonNegativeNumber(writeBufferSizeInBytes, nameof(writeBufferSizeInBytes));
            ReadBufferSizeInBytes = AssertNonNegativeNumber(readBufferSizeInBytes, nameof(readBufferSizeInBytes));
        }

        private int AssertNonNegativeNumber(int bufferSize, string nameOfParameter)
        {
            if (bufferSize <= 0)
            {
                throw new TusConfigurationException($"{nameOfParameter} is zero or a negative number");
            }

            return bufferSize;
        }
    }
}
