#if pipelines

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Models.PipeReaders
{
    internal class ChecksumAwarePipeReader : PipeReader
    {
        private readonly PipeReader _backingReader;

        public ChecksumAwarePipeReader(PipeReader backingReader, Checksum checksum)
        {
            _backingReader = backingReader;
            Checksum = checksum;
        }

        public Checksum Checksum { get; }

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
            _backingReader.CancelPendingRead();
        }

        public override void Complete(Exception exception = null)
        {
            _backingReader.Complete(exception);
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            return _backingReader.ReadAsync(cancellationToken);
        }

        public override bool TryRead(out ReadResult result)
        {
            return _backingReader.TryRead(out result);
        }
    }
}


#endif