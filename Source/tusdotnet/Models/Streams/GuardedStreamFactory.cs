using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions.Internal;
using tusdotnet.Models;

namespace tusdotnet.IntentHandlers
{
    internal static class GuardedStreamFactory
    {
        internal static async Task<Stream> Create(ContextAdapter context)
        {
            var maxSizeData = await context.GetMaxReadSizeInfo();
            var guardedStream = new ClientDisconnectGuardedReadOnlyStream(context.Request.Body, context.ClientDisconnectGuard);

            if (maxSizeData.MaxReadSize is null)
                return guardedStream;

            var maxSizeStream = new MaxReadSizeGuardedReadOnlyStream(guardedStream, maxSizeData.PreviouslyRead, maxSizeData.MaxReadSize.Value, maxSizeData.SizeSource);
            return maxSizeStream;
        }
    }
}
