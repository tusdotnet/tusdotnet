#if pipelines

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Models.PipeReaders
{
    /// <summary>
    /// Stops reading the request body once the file lock is lost, so a store that never checks the lock-lost token
    /// still stops writing. It cannot undo data the store wrote before the lock was lost. A store that must reject
    /// that data uses a conditional write against the storage.
    /// </summary>
    internal class FileLockLostGuardedPipeReader : PipeReader
    {
        private readonly PipeReader _backingReader;

        public FileLockLostGuardedPipeReader(
            PipeReader backingReader,
            CancellationToken lockLostToken
        )
        {
            _backingReader = backingReader;
            LockLostToken = lockLostToken;
        }

        public CancellationToken LockLostToken { get; }

        // A store that commits after AdvanceTo on the last read would otherwise not notice a lock lost after that
        // read returned.
        public override void AdvanceTo(SequencePosition consumed)
        {
            ThrowIfLockLost();
            _backingReader.AdvanceTo(consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            ThrowIfLockLost();
            _backingReader.AdvanceTo(consumed, examined);
        }

        public override void CancelPendingRead()
        {
            _backingReader.CancelPendingRead();
        }

        public override void Complete(Exception exception = null)
        {
            _backingReader.Complete(exception);
        }

        public override async ValueTask<ReadResult> ReadAsync(
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfLockLost();

            ReadResult result;

            // Pass the lock-lost token to the inner read, so a read waiting on a slow client stops when the lock
            // is lost instead of when the next bytes arrive.
            using (
                var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    LockLostToken
                )
            )
            {
                try
                {
                    result = await _backingReader.ReadAsync(cts.Token);
                }
                catch (OperationCanceledException) when (LockLostToken.IsCancellationRequested)
                {
                    throw new FileLockLostException();
                }
            }

            // Another request may own the file now, so data read after the lock was lost must not reach the store.
            ThrowIfLockLost();

            return result;
        }

        public override bool TryRead(out ReadResult result)
        {
            ThrowIfLockLost();

            var hasData = _backingReader.TryRead(out result);

            ThrowIfLockLost();

            return hasData;
        }

        private void ThrowIfLockLost()
        {
            if (LockLostToken.IsCancellationRequested)
            {
                throw new FileLockLostException();
            }
        }
    }
}

#endif
