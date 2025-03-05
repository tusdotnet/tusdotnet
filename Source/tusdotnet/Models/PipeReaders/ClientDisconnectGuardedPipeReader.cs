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
        private readonly ClientDisconnectGuardWithTimeout _clientDisconnectGuard;
        private readonly ReadOnlySequence<byte> _emptySequence = new();

        public ClientDisconnectGuardedPipeReader(
            PipeReader backingReader,
            ClientDisconnectGuardWithTimeout clientDisconnectGuard
        )
        {
            _backingReader = backingReader;
            _clientDisconnectGuard = clientDisconnectGuard;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            _clientDisconnectGuard.Execute(
                () => _backingReader.AdvanceTo(consumed),
                _clientDisconnectGuard.GuardedToken
            );
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            _clientDisconnectGuard.Execute(
                () => _backingReader.AdvanceTo(consumed, examined),
                _clientDisconnectGuard.GuardedToken
            );
        }

        public override void CancelPendingRead()
        {
            throw new NotImplementedException();
        }

        public override void Complete(Exception exception = null)
        {
            _clientDisconnectGuard.Execute(
                () => _backingReader.Complete(exception),
                _clientDisconnectGuard.GuardedToken
            );
        }

        public override async ValueTask<ReadResult> ReadAsync(
            CancellationToken cancellationToken = default
        )
        {
            return await _clientDisconnectGuard.Execute(
                guardFromClientDisconnect: async () =>
                    await _backingReader.ReadAsync(cancellationToken),
                getDefaultValue: () =>
                    new ReadResult(_emptySequence, isCanceled: true, isCompleted: false),
                cancellationToken
            );
        }

        public override bool TryRead(out ReadResult result)
        {
            throw new NotImplementedException();
        }
    }
}

#endif
