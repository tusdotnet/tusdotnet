#if pipelines

using System.IO.Pipelines;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions.Internal;

namespace tusdotnet.Models.PipeReaders
{
    internal static class GuardedPipeReaderFactory
    {
        internal static async Task<PipeReader> Create(ContextAdapter context)
        {
            var maxSizeData = await context.GetMaxReadSizeInfo();
            var guardedPipeReader = new ClientDisconnectGuardedPipeReader(context.Request.BodyReader, context.CancellationToken);

            if (maxSizeData.MaxReadSize is null)
            {
                return guardedPipeReader;
            }

            var maxSizeReader = new MaxReadSizeGuardedPipeReader(guardedPipeReader, maxSizeData.PreviouslyRead, maxSizeData.MaxReadSize.Value, maxSizeData.SizeSource);
            return maxSizeReader;
        }
    }
}

#endif