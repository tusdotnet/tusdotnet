using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.test.Helpers
{
    /// <summary>
    /// The SlowMemoryStream adds a 100 ms delay on every ReadAsync.
    /// </summary>
    public class SlowMemoryStream : Stream
    {
        private readonly MemoryStream _stream;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="buffer">The buffer to read from</param>
        public SlowMemoryStream(byte[] buffer)
        {
            _stream = new MemoryStream(buffer);
        }

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => _stream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _stream.Length;

        public override long Position
        {
            get { return _stream.Position; }
            set { _stream.Position = value; }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(100);

            // Note: Do not use the provided cancellation token but let the disk store cancel the read instead.
            return await _stream.ReadAsync(buffer, offset, count, CancellationToken.None);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Flush() => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }
}
