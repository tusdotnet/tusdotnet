using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Models.Streams
{
    internal class ChecksumAwareStream : ReadOnlyStream
    {
        public ChecksumAwareStream(Stream backingStream, Checksum checksum) : base(backingStream)
        {
            Checksum = checksum;
        }

        public Checksum Checksum { get; }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return BackingStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }
}
