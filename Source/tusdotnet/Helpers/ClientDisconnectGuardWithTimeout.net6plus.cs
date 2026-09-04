#if NET6_0_OR_GREATER

using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal partial class ClientDisconnectGuardWithTimeout
    {
        private void ExecuteWithTimeout(Action guardFromClientDisconnect)
        {
            _cts.CancelAfter(_executionTimeout);
            guardFromClientDisconnect();
            _cts.TryReset();
        }

        private async Task<T> ExecuteWithTimeout<T>(Func<Task<T>> guardFromClientDisconnect)
        {
            _cts.CancelAfter(_executionTimeout);
            var res = await guardFromClientDisconnect();
            _cts.TryReset();
            return res;
        }

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

        private void ExecuteWithTimeout<TState, TResult>(
            TState state,
            Func<TState, TResult> operation
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
