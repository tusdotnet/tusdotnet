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
            if (context.StoreAdapter.Extensions.Checksum)
            {
                // TODO: should be cached once parsed
                var checksum = !string.IsNullOrEmpty(context.Request.Headers.UploadChecksum) ? new Checksum(context.Request.Headers.UploadChecksum) : null;
                if (checksum?.IsValid == true)
                {
                    reader = new ChecksumAwarePipeReader(reader, checksum);
                }
            }

            return reader;
        }
    }
}

#endif