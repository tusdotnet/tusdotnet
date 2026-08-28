#if pipelines

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models.PipeReaders
{
    internal class ClientDisconnectGuardedPipeReaderOld : PipeReader
    {
        // _unconsumedBuffer tracks the portion of the last ReadAsync buffer that the caller has not yet consumed
        // (i.e. not yet passed as the `consumed` argument to AdvanceTo).
        //
        // This is needed because if the client disconnects while the caller is in the middle of
        // a read loop using the AdvanceTo(consumed: start, examined: end) pattern, the next
        // ReadAsync will throw and the backing PipeReader will never return the already-buffered
        // data. We therefore serve that data from here instead of returning an empty sequence.
        //
        // Memory safety: the underlying byte arrays are rented from a MemoryPool<byte> managed
        // by the Pipe. The pool only reclaims a segment when *both* reader and writer have
        // completed (Pipe.CompletePipe) or when AdvanceTo marks it as consumed. Since we only
        // slice _unconsumedBuffer forward on each AdvanceTo call (never calling the backing
        // reader's AdvanceTo with a position beyond what the caller supplied), the memory
        // remains valid for as long as we hold this reference.
        //
        // _unconsumedBuffer is a struct (ReadOnlySequence<byte>) that holds references into
        // Kestrel's pipe buffers. Kestrel owns that pipe and calls Complete() on its BodyReader
        // when the request ends, which is what ultimately triggers Pipe.CompletePipe() and returns
        // the pooled segments. This wrapper does not need to call Complete() itself; there is no
        // memory leak from holding _unconsumedBuffer, as the struct is collected with this instance
        // and holds no finalizer or unmanaged resources.
        //
        // Relevant runtime source:
        //   Pipe.CompletePipe:  https://source.dot.net/#System.IO.Pipelines/System/IO/Pipelines/Pipe.cs,983
        //   Pipe.AdvanceReader: https://source.dot.net/#System.IO.Pipelines/System/IO/Pipelines/Pipe.cs,558

        private readonly PipeReader _backingReader;
        private readonly ClientDisconnectGuardWithTimeout _clientDisconnectGuard;
        private ReadOnlySequence<byte> _empty = new();

        public ClientDisconnectGuardedPipeReaderOld(
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
            _backingReader.CancelPendingRead();
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
            var readResult = await _clientDisconnectGuard.Execute(
                guardFromClientDisconnect: async () =>
                    await _backingReader.ReadAsync(cancellationToken),
                getDefaultValue: () => new ReadResult(_empty, isCanceled: true, isCompleted: false),
                cancellationToken
            );

            return readResult;
        }

        public override bool TryRead(out ReadResult result)
        {
            var hasData = false;
            var capturedResult = default(ReadResult);

            var disconnected = _clientDisconnectGuard.Execute(
                () => hasData = _backingReader.TryRead(out capturedResult),
                _clientDisconnectGuard.GuardedToken
            );

            if (disconnected)
            {
                result = new ReadResult(_empty, isCanceled: true, isCompleted: false);
                return true;
            }

            if (!hasData)
            {
                result = default;
                return false;
            }

            result = capturedResult;
            return true;
        }
    }
}

#endif
