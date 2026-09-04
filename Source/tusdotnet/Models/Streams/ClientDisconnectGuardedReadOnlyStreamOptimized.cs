using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;

namespace tusdotnet.Models.Streams
{
    /// <summary>
    /// Allocation-reduced variant of <see cref="ClientDisconnectGuardedReadOnlyStream"/> that
    /// uses the state-based <c>Execute&lt;TState, TResult&gt;</c> overload together with
    /// <c>static</c> lambdas to eliminate closure allocations on every <c>ReadAsync</c> call.
    ///
    /// Note: the underlying <c>Stream.ReadAsync</c> still allocates a <c>Task&lt;int&gt;</c>
    /// on each call — Stream APIs do not support <c>ValueTask</c>. The saving here is the
    /// elimination of the two delegate wrappers and their captured variables.
    /// </summary>
    internal class ClientDisconnectGuardedReadOnlyStreamOptimized : ReadOnlyStream
    {
        private readonly ClientDisconnectGuardWithTimeout _clientDisconnectGuard;

        internal ClientDisconnectGuardedReadOnlyStreamOptimized(
            Stream backingStream,
            ClientDisconnectGuardWithTimeout clientDisconnectGuard
        )
            : base(backingStream)
        {
            _clientDisconnectGuard = clientDisconnectGuard;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            var result = await _clientDisconnectGuard.ExecuteAsync(
                state: (stream: BackingStream, buffer, offset, count),
                operation: static async (s, ct) =>
                {
                    var bytesRead = await s.stream.ReadAsync(s.buffer, s.offset, s.count, ct);
                    return new ClientDisconnectGuardReadStreamAsyncResult(false, bytesRead);
                },
                getDefaultValue: static _ => new ClientDisconnectGuardReadStreamAsyncResult(true, 0),
                guardedToken: cancellationToken
            );

            return result.BytesRead;
        }
    }
}
