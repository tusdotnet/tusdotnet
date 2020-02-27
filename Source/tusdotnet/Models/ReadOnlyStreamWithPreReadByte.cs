using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Models
{
    internal class ReadOnlyStreamWithPreReadByte : ReadOnlyStream
    {
        private readonly byte _preReadByte;
        private bool _hasWrittenPreReadByte;

        internal ReadOnlyStreamWithPreReadByte(Stream backingStream, byte preReadByte)
            : base(backingStream)
        {
            _preReadByte = preReadByte;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_hasWrittenPreReadByte)
            {
                _hasWrittenPreReadByte = true;

                var localBuffer = ArrayPool<byte>.Shared.Rent(count);

                try
                {
                    var read = await BackingStream.ReadAsync(localBuffer, 0, count - 1);

                    buffer[0] = _preReadByte;
                    for (var i = 0; i < read; i++)
                    {
                        buffer[i + 1] = localBuffer[i];
                    }

                    return read + 1;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(localBuffer);
                }
            }

            return await BackingStream.ReadAsync(buffer, offset, count);
        }
    }
}
