#if pipelines

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Models.PipeReaders
{
    internal class MaxReadSizeGuardedPipeReader : PipeReader
    {
        private readonly PipeReader _backingReader;
        private long _totalCommittedBytes;
        private long _bytesReadSinceLastAdvance;
        private readonly long _maxSizeToRead;
        private readonly MaxReadSizeExceededException.SizeSourceType _sizeSource;

        public MaxReadSizeGuardedPipeReader(
            PipeReader backingReader,
            long startCountingFrom,
            long maxSizeToRead,
            MaxReadSizeExceededException.SizeSourceType sizeSource)
        {
            _backingReader = backingReader;
            _totalCommittedBytes = startCountingFrom;
            _maxSizeToRead = maxSizeToRead;
            _sizeSource = sizeSource;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            // Save previously read bytes to total amount.
            _totalCommittedBytes += _bytesReadSinceLastAdvance;
            _backingReader.AdvanceTo(consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            _backingReader.AdvanceTo(consumed, examined);
        }

        public override void CancelPendingRead()
        {
            throw new NotImplementedException();
        }

        public override void Complete(Exception exception = null)
        {
            _backingReader.Complete(exception);
        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            var result = await _backingReader.ReadAsync(cancellationToken);

            // Before a call to AdvanceTo(SequencePosition consumed) have been made
            // the buffer contains _all_ data over several reads.
            _bytesReadSinceLastAdvance = result.Buffer.Length;

            if (_totalCommittedBytes + _bytesReadSinceLastAdvance > _maxSizeToRead)
            {
                throw new MaxReadSizeExceededException(_sizeSource);
            }

            return result;
        }

        public override bool TryRead(out ReadResult result)
        {
            throw new NotImplementedException();
        }
    }
}

#endif