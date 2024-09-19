using System.IO;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions.Internal;
using tusdotnet.Models;
using tusdotnet.Models.Streams;

namespace tusdotnet.IntentHandlers
{
    internal static class GuardedStreamFactory
    {
        internal static async Task<Stream> Create(ContextAdapter context)
        {
            Stream guardedStream = new ClientDisconnectGuardedReadOnlyStream(context.Request.Body, context.ClientDisconnectGuard);

            guardedStream = await WrapWithMaxSize(guardedStream, context);
            guardedStream = WrapWithChecksumInfo(guardedStream, context);

            return guardedStream;
        }

        private static async Task<Stream> WrapWithMaxSize(Stream stream, ContextAdapter context)
        {
            var maxSizeData = await context.GetMaxReadSizeInfo();
            if (maxSizeData.MaxReadSize is not null)
            {
                stream = new MaxReadSizeGuardedReadOnlyStream(stream, maxSizeData.PreviouslyRead, maxSizeData.MaxReadSize.Value, maxSizeData.SizeSource);
            }

            return stream;
        }

        private static Stream WrapWithChecksumInfo(Stream stream, ContextAdapter context)
        {
            if (context.StoreAdapter.Extensions.Checksum)
            {
                // TODO: should be cached once parsed
                var checksum = !string.IsNullOrEmpty(context.Request.Headers.UploadChecksum) ? new Checksum(context.Request.Headers.UploadChecksum) : null;
                if (checksum?.IsValid == true)
                {
                    stream = new ChecksumAwareStream(stream, checksum);
                }
            }

            return stream;
        }
    }
}
