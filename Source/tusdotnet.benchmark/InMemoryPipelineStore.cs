using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.benchmark
{
    public class InMemoryPipelineStore : InMemoryStore, ITusPipelineStore
    {
        public async Task<long> AppendDataAsync(string fileId, PipeReader pipeReader, CancellationToken cancellationToken)
        {
            long bytesWritten = 0;

            while (true)
            {
                ReadResult result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition position = buffer.Start;
                SequencePosition consumed = position;

                try
                {
                    if (result.IsCanceled)
                    {
                        throw new Exception("Read canceled");
                    }

                    while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory))
                    {
                        await Data[fileId].WriteAsync(memory, cancellationToken).ConfigureAwait(false);

                        bytesWritten += memory.Length;
                        consumed = position;
                    }

                    // The while loop completed succesfully, so we've consumed the entire buffer.
                    consumed = buffer.End;

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                finally
                {
                    // Advance even if WriteAsync throws so the PipeReader is not left in the
                    // currently reading state
                    pipeReader.AdvanceTo(consumed);
                }
            }

            return bytesWritten;
        }
    }
}
