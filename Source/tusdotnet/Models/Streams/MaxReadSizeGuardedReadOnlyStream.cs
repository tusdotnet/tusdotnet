using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Models
{
    internal class MaxReadSizeGuardedReadOnlyStream : ReadOnlyStream
    {
        private readonly long _maxSizeToRead;
        private readonly MaxReadSizeExceededException.SizeSourceType _sizeSource;
        private long _totalReadBytes;

        public MaxReadSizeGuardedReadOnlyStream(
            Stream backingStream, 
            long startCountingFrom, 
            long maxSizeToRead,
            MaxReadSizeExceededException.SizeSourceType sizeSource) : base(backingStream)
        {
            _maxSizeToRead = maxSizeToRead;
            _sizeSource = sizeSource;
            _totalReadBytes = startCountingFrom;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await BackingStream.ReadAsync(buffer, offset, count, cancellationToken);

            _totalReadBytes += read;

            if (_totalReadBytes > _maxSizeToRead)
            {
                throw new MaxReadSizeExceededException(_sizeSource);
            }

            return read;
        }
    }
}
