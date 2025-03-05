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
