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
            PipeReader reader = new ClientDisconnectGuardedPipeReader(context.Request.BodyReader, context.ClientDisconnectGuard);

            reader = await WrapWithMaxSize(reader, context);
            reader = WrapWithChecksumInfo(reader, context);

            return reader;
        }

        private static async Task<PipeReader> WrapWithMaxSize(PipeReader reader, ContextAdapter context)
        {
            var maxSizeData = await context.GetMaxReadSizeInfo();
            if (maxSizeData.MaxReadSize is not null)
            {
                reader = new MaxReadSizeGuardedPipeReader(reader, maxSizeData.PreviouslyRead, maxSizeData.MaxReadSize.Value, maxSizeData.SizeSource);
            }

            return reader;
        }

        private static PipeReader WrapWithChecksumInfo(PipeReader reader, ContextAdapter context)
        {
            if (context.StoreAdapter.Extensions.Checksum && context.Cache.UploadChecksum?.IsValid == true)
            {
                reader = new ChecksumAwarePipeReader(reader, context.Cache.UploadChecksum);
            }

            return reader;
        }
    }
}

#endif