#if NET6_0_OR_GREATER

using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal partial class ClientDisconnectGuardWithTimeout
    {
        private async Task<TResult> ExecuteWithTimeout<TState, TResult>(
            TState state,
            Func<TState, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken
        )
        {
            _cts.CancelAfter(_executionTimeout);
            var res = await operation(state, cancellationToken);
            _cts.TryReset();
            return res;
        }

#if pipelines
        private async ValueTask<TResult> ExecuteWithTimeout<TState, TResult>(
            TState state,
            Func<TState, CancellationToken, ValueTask<TResult>> operation,
            CancellationToken cancellationToken
        )
        {
            _cts.CancelAfter(_executionTimeout);
            var res = await operation(state, cancellationToken);
            _cts.TryReset();
            return res;
        }

        private TResult ExecuteWithTimeout<TState, TResult>(
            TState state,
            Func<TState, TResult> operation
        )
        {
            _cts.CancelAfter(_executionTimeout);
            var res = operation(state);
            _cts.TryReset();
            return res;
        }

        private void ExecuteWithTimeout<TState>(
            TState state,
            Action<TState> operation
        )
        {
            _cts.CancelAfter(_executionTimeout);
            operation(state);
            _cts.TryReset();
        }
#endif
    }
}

#endif
