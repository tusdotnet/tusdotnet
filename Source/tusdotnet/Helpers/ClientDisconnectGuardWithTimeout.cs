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

#if pipelines

        internal bool Execute(Action guardFromClientDisconnect, CancellationToken guardedToken)
        {
            try
            {
                ExecuteWithTimeout(guardFromClientDisconnect);
                return false;
            }
            catch (Exception ex) when (ClientDisconnected(ex, guardedToken))
            {
                return true;
            }
        }
#endif

        internal async Task<T> Execute<T>(
            Func<Task<T>> guardFromClientDisconnect,
            Func<T> getDefaultValue,
            CancellationToken guardedToken
        )
        {
            try
            {
                return await ExecuteWithTimeout(guardFromClientDisconnect);
            }
            catch (Exception exc) when (ClientDisconnected(exc, guardedToken))
            {
                return getDefaultValue();
            }
        }

        /// <summary>
        /// Reduced-allocation overload for async Task-returning operations. Accepts static
        /// delegates together with a captured <paramref name="state"/> value so that no
        /// closure class is allocated on each call. The backing <c>Task&lt;TResult&gt;</c>
        /// itself still allocates (Stream APIs do not support <c>ValueTask</c>), but the
        /// two delegate wrappers and their captures are eliminated.
        /// </summary>
        internal async Task<TResult> ExecuteAsync<TState, TResult>(
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
        /// <summary>
        /// Allocation-free overload for async operations. The static-lambda calling convention
        /// means <paramref name="operation"/> and <paramref name="getDefaultValue"/> are compiled
        /// to singleton delegates — no closure class is allocated on each call.
        /// </summary>
        internal async ValueTask<TResult> Execute<TState, TResult>(
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

        /// <summary>
        /// Allocation-free overload for synchronous operations. Use a dummy return value (e.g.
        /// <c>false</c>) for void-style callers such as <c>AdvanceTo</c> and <c>Complete</c>.
        /// </summary>
        internal TResult Execute<TState, TResult>(
            TState state,
            Func<TState, TResult> operation,
            CancellationToken guardedToken
        )
        {
            try
            {
                ExecuteWithTimeout(state, operation);
                return default;
            }
            catch (Exception exc) when (ClientDisconnected(exc, guardedToken))
            {
                return default;
            }
        }
#endif

        /// <summary>
        /// Returns true if the client disconnected, otherwise false.
        /// </summary>
        /// <param name="exception">The exception retrieved from the operation that might have been caused by a client disconnect</param>
        /// <param name="cancellationToken">The client's request cancellation token</param>
        /// <returns>True if the client disconnected, otherwise false</returns>
        private bool ClientDisconnected(Exception exception, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return true;
            }

            var exceptionFullName = exception.GetType().FullName;

            // IsCancellationRequested is false when connecting directly to Kestrel in ASP.NET Core 1.1 (on netcoreapp1_1).
            // Instead the exception below is thrown.
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
