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
        private long _totalBytesRead;
        private readonly long _maxSizeToRead;
        private readonly MaxReadSizeExceededException.SizeSourceType _sizeSource;

        public MaxReadSizeGuardedPipeReader(
            PipeReader backingReader,
            long startCountingFrom,
            long maxSizeToRead,
            MaxReadSizeExceededException.SizeSourceType sizeSource)
        {
            _backingReader = backingReader;
            _totalBytesRead = startCountingFrom;
            _maxSizeToRead = maxSizeToRead;
            _sizeSource = sizeSource;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
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
            _totalBytesRead += result.Buffer.Length;

            if (_totalBytesRead > _maxSizeToRead)
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