using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models
{
    internal class ClientDisconnectGuardedReadOnlyStream : Stream
    {
        public override bool CanRead => _backingStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _backingStream.Length;

        public override long Position { get => _backingStream.Position; set => throw new NotSupportedException(); }

        internal CancellationToken CancellationToken { get; }

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Stream _backingStream;

        /// <summary>
        /// Default ctor
        /// </summary>
        /// <param name="backingStream">The stream to guard against client disconnects</param>
        /// <param name="cancellationTokenSource">Token source to cancel when the client disconnects. Preferably use CancellationTokenSource.CreateLinkedTokenSource(RequestCancellationToken).</param>
        internal ClientDisconnectGuardedReadOnlyStream(Stream backingStream, CancellationTokenSource cancellationTokenSource)
        {
            CancellationToken = cancellationTokenSource.Token;

            _cancellationTokenSource = cancellationTokenSource;
            _backingStream = backingStream;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var result = await ClientDisconnectGuard.ReadStreamAsync(_backingStream, buffer, offset, count, cancellationToken);

            if (result.ClientDisconnected)
            {
                _cancellationTokenSource.Cancel();
                return 0;
            }

            return result.BytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}