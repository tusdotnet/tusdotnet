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
        internal static async Task<Tuple<Stream, CancellationToken>> Create(ContextAdapter context)
        {
            var maxSizeData = await context.GetMaxReadSizeInfo();
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            var guardedStream = new ClientDisconnectGuardedReadOnlyStream(context.Request.Body, cancellationTokenSource);

            if (maxSizeData.MaxReadSize is null)
                return new(guardedStream, guardedStream.CancellationToken);

            var maxSizeStream = new MaxReadSizeGuardedReadOnlyStream(guardedStream, maxSizeData.PreviouslyRead, maxSizeData.MaxReadSize.Value, maxSizeData.SizeSource);
            return new(maxSizeStream, guardedStream.CancellationToken);
        }
    }
}
