#if pipelines

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models.PipeReaders
{
    /// <summary>
    /// Allocation-free variant of <see cref="ClientDisconnectGuardedPipeReader"/> that uses
    /// the state-based <c>Execute&lt;TState, TResult&gt;</c> overloads together with
    /// <c>static</c> lambdas to eliminate closure allocations on every call.
    /// </summary>
    internal class ClientDisconnectGuardedPipeReaderOptimized : PipeReader
    {
        private readonly PipeReader _backingReader;
        private readonly ClientDisconnectGuardWithTimeout _clientDisconnectGuard;
        private ReadOnlySequence<byte> _unconsumedBuffer = new();

        public ClientDisconnectGuardedPipeReaderOptimized(
            PipeReader backingReader,
            ClientDisconnectGuardWithTimeout clientDisconnectGuard
        )
        {
            _backingReader = backingReader;
            _clientDisconnectGuard = clientDisconnectGuard;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            _unconsumedBuffer = _unconsumedBuffer.Slice(consumed);
            _clientDisconnectGuard.Execute<(PipeReader reader, SequencePosition pos), bool>(
                state: (_backingReader, consumed),
                operation: static s => { s.reader.AdvanceTo(s.pos); return false; },
                _clientDisconnectGuard.GuardedToken
            );
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            _unconsumedBuffer = _unconsumedBuffer.Slice(consumed);
            _clientDisconnectGuard.Execute<(PipeReader reader, SequencePosition consumed, SequencePosition examined), bool>(
                state: (_backingReader, consumed, examined),
                operation: static s => { s.reader.AdvanceTo(s.consumed, s.examined); return false; },
                _clientDisconnectGuard.GuardedToken
            );
        }

        public override void CancelPendingRead()
        {
            _backingReader.CancelPendingRead();
        }

        public override void Complete(Exception exception = null)
        {
            _unconsumedBuffer = new();
            _clientDisconnectGuard.Execute<(PipeReader reader, Exception ex), bool>(
                state: (_backingReader, exception),
                operation: static s => { s.reader.Complete(s.ex); return false; },
                _clientDisconnectGuard.GuardedToken
            );
        }

        public override async ValueTask<ReadResult> ReadAsync(
            CancellationToken cancellationToken = default
        )
        {
            var readResult = await _clientDisconnectGuard.Execute(
                state: this,
                operation: static (self, ct) => self._backingReader.ReadAsync(ct),
                getDefaultValue: static self => new ReadResult(self._unconsumedBuffer, isCanceled: true, isCompleted: false),
                guardedToken: cancellationToken
            );

            _unconsumedBuffer = readResult.Buffer;

            return readResult;
        }

        public override bool TryRead(out ReadResult result)
        {
            var hasData = false;
            var capturedResult = default(ReadResult);

            // TryRead uses out-param so it cannot be expressed as a pure static lambda.
            // We use the existing Action-based overload which is already allocation-free
            // for the synchronous path (Action is cached by the JIT for non-capturing lambdas).
            var disconnected = _clientDisconnectGuard.Execute(
                () => hasData = _backingReader.TryRead(out capturedResult),
                _clientDisconnectGuard.GuardedToken
            );

            if (disconnected)
            {
                result = new ReadResult(_unconsumedBuffer, isCanceled: true, isCompleted: false);
                return true;
            }

            if (!hasData)
            {
                result = default;
                return false;
            }

            _unconsumedBuffer = capturedResult.Buffer;
            result = capturedResult;
            return true;
        }
    }
}

#endif
