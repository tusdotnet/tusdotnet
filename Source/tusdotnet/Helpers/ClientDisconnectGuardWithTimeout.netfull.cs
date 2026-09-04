#if netfull

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal partial class ClientDisconnectGuardWithTimeout
    {
        private Task<T> ExecuteWithTimeout<T>(Func<Task<T>> guardFromClientDisconnect)
        {
            // NOTE: Do not await here to hide ExecuteWithTimeout from stacktraces.
            return guardFromClientDisconnect();
        }

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
