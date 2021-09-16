using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.test.Helpers
{
    internal sealed class RequestStreamFake : MemoryStream
    {
        internal delegate Task<int> OnReadAsync(
            RequestStreamFake stream,
            byte[] bufferToFill,
            int offset,
            int count,
            CancellationToken cancellationToken);

        private readonly OnReadAsync _onReadAsync;

        public RequestStreamFake(OnReadAsync onReadAsync, byte[] data) : base(data)
        {
            _onReadAsync = onReadAsync;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _onReadAsync(this, buffer, offset, count, cancellationToken);
        }

#if pipelines

        // Method use by PipeReader when the store implements ITusPipelinesStore.
        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            var size = await _onReadAsync(this, destination.ToArray(), 0, destination.Length, cancellationToken);

            return size;
        }

#endif

        public Task<int> ReadBackingStreamAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }
}
