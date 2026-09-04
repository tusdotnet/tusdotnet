using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal partial class ClientDisconnectGuardWithTimeout
    {
        private readonly CancellationTokenSource _cts;
        private readonly TimeSpan _executionTimeout;

        internal CancellationToken GuardedToken { get; }

        internal ClientDisconnectGuardWithTimeout(
            TimeSpan executionTimeout,
            CancellationToken tokenToMonitor
        )
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(tokenToMonitor);
            _executionTimeout = executionTimeout;

            GuardedToken = _cts.Token;
        }

        internal async Task<TResult> ExecuteTask<TState, TResult>(
            TState state,
            Func<TState, CancellationToken, Task<TResult>> operation,
            Func<TState, TResult> getDefaultValue,
            CancellationToken guardedToken
        )
        {
            try
            {
                return await ExecuteWithTimeout(state, operation, guardedToken);
            }
            catch (Exception exc) when (ClientDisconnected(exc, guardedToken))
            {
                return getDefaultValue(state);
            }
        }

#if pipelines

        internal async ValueTask<TResult> ExecuteValueTask<TState, TResult>(
            TState state,
            Func<TState, CancellationToken, ValueTask<TResult>> operation,
            Func<TState, TResult> getDefaultValue,
            CancellationToken guardedToken
        )
        {
            try
            {
                return await ExecuteWithTimeout(state, operation, guardedToken);
            }
            catch (Exception exc) when (ClientDisconnected(exc, guardedToken))
            {
                return getDefaultValue(state);
            }
        }

        internal void Execute<TState>(
            TState state,
            Action<TState> operation,
            CancellationToken guardedToken
        )
        {
            try
            {
                ExecuteWithTimeout(state, operation);
            }
            catch (Exception exc) when (ClientDisconnected(exc, guardedToken)) { }
        }

        internal TResult Execute<TState, TResult>(
            TState state,
            Func<TState, TResult> operation,
            Func<TState, TResult> getDefaultValue,
            CancellationToken guardedToken
        )
        {
            try
            {
                return ExecuteWithTimeout(state, operation);
            }
            catch (Exception exc) when (ClientDisconnected(exc, guardedToken))
            {
                return getDefaultValue(state);
            }
        }
#endif

        private bool ClientDisconnected(Exception exception, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return true;
            }

            var exceptionFullName = exception.GetType().FullName;

            // IsCancellationRequested is false when connecting directly to Kestrel in ASP.NET Core 1.1 (on netcoreapp1_1).
            if (exceptionFullName == "Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException")
            {
                _cts.Cancel();
                return true;
            }

            // IsCancellationRequested is false in some scenarios when connecting directly to Kestrel in ASP.NET Core 3.1 (on netcoreapp3_1).
            if (exceptionFullName == "Microsoft.AspNetCore.Connections.ConnectionResetException")
            {
                _cts.Cancel();
                return true;
            }

            return false;
        }
    }
}
