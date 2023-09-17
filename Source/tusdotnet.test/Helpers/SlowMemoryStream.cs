using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.test.Helpers
{
    /// <summary>
    /// The SlowMemoryStream adds a delay on every ReadAsync.
    /// </summary>
    public class SlowMemoryStream : Stream
    {
        private readonly MemoryStream _stream;
        private readonly int _delayPerReadInMs;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="buffer">The buffer to read from</param>
        public SlowMemoryStream(byte[] buffer, int delayPerReadInMs = 100)
        {
            _stream = new MemoryStream(buffer);
            _delayPerReadInMs = delayPerReadInMs;
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
            await Task.Delay(_delayPerReadInMs);

            // Note: Some of our tests require that we get the data back to verify that things worked.
            // Ignore the cancellation token here to let this happen.
            return await _stream.ReadAsync(buffer, offset, count, CancellationToken.None);
        }

#if netstandard

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delayPerReadInMs);

            // Note: Some of our tests require that we get the data back to verify that things worked.
            // Ignore the cancellation token here to let this happen.
            return await _stream.ReadAsync(buffer, CancellationToken.None);
        }

#endif

        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Flush() => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }
}
