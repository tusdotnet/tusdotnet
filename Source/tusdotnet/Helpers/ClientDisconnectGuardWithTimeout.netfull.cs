#if netfull

using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal partial class ClientDisconnectGuardWithTimeout
    {
        private Task<TResult> ExecuteWithTimeout<TState, TResult>(
            TState state,
            Func<TState, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken
        )
        {
            // NOTE: Do not await here to hide ExecuteWithTimeout from stacktraces.
            return operation(state, cancellationToken);
        }
    }
}

#endif
