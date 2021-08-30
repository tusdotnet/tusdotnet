#if pipelines

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models
{
    internal class ClientDisconnectGuardedPipeReader : PipeReader
    {
        private readonly PipeReader _backingReader;
        private readonly CancellationToken _cancellationToken;
        private readonly ReadOnlySequence<byte> _emptySequence = new(Array.Empty<byte>());

        public ClientDisconnectGuardedPipeReader(PipeReader backingReader, CancellationToken cancellation)
        {
            _backingReader = backingReader;
            _cancellationToken = cancellation;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            ClientDisconnectGuard.Execute(() => _backingReader.AdvanceTo(consumed), _cancellationToken);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            ClientDisconnectGuard.Execute(() => _backingReader.AdvanceTo(consumed, examined), _cancellationToken);
        }

        public override void CancelPendingRead()
        {
            throw new NotImplementedException();
        }

        public override void Complete(Exception exception = null)
        {
            ClientDisconnectGuard.Execute(() => _backingReader.Complete(exception), _cancellationToken);
        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _backingReader.ReadAsync(_cancellationToken);
            }
            catch (Exception exc) when (ClientDisconnectGuard.ClientDisconnected(exc, cancellationToken))
            {
                return new ReadResult(_emptySequence, isCanceled: true, isCompleted: false);
            }
        }

        public override bool TryRead(out ReadResult result)
        {
            throw new NotImplementedException();
        }
    }
}

#endif