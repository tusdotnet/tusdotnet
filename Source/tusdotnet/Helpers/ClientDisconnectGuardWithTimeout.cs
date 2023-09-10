#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal class ClientDisconnectGuardWithTimeout
    {
        private readonly CancellationTokenSource _cts;
        private readonly TimeSpan _executionTimeout;

        internal CancellationToken GuardedToken { get; }

        internal ClientDisconnectGuardWithTimeout(CancellationToken tokenToMonitor, TimeSpan executionTimeout)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(tokenToMonitor);
            _executionTimeout = executionTimeout;

            GuardedToken = _cts.Token;
        }

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

        internal async Task<T> Execute<T>(Func<Task<T>> guardFromClientDisconnect, Func<T> getDefaultValue, CancellationToken guardedToken)
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
        private static bool ClientDisconnected(Exception exception, CancellationToken cancellationToken)
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
                return true;
            }

            // IsCancellationRequested is false in some scenarios when connecting directly to Kestrel in ASP.NET Core 3.1 (on netcoreapp3_1).
            if (exceptionFullName == "Microsoft.AspNetCore.Connections.ConnectionResetException")
            {
                return true;
            }

            return false;
        }

#if NET6_0_OR_GREATER

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

#else 
        private void ExecuteWithTimeout(Action guardFromClientDisconnect)
        {
            var completed = false;
            var timeout = Task.Delay(_executionTimeout);
            timeout.ContinueWith(x =>
            {
                if (completed) return;

                _cts.Cancel();
            });

            guardFromClientDisconnect();
            completed = true;
        }

        private async Task<T> ExecuteWithTimeout<T>(Func<Task<T>> guardFromClientDisconnect)
        {
            var completed = false;
            var timeout = Task.Delay(_executionTimeout);
            timeout.ContinueWith(x =>
            {
                if (completed) return;

                _cts.Cancel();
            });

            var res = await guardFromClientDisconnect();

            completed = true;

            return res;
        }


#endif

        //internal ClientDisconnectGuardWithTimeout(
        //    CancellationTokenSource sourceToCancelWhenDisconnectDetected,
        //    CancellationToken tokenToMonitor,
        //    TimeSpan timeout)
        //{
        //    _sourceToCancelWhenDisconnectDetected = sourceToCancelWhenDisconnectDetected;
        //    _tokenToMonitor = tokenToMonitor;
        //    _timeout = timeout;

        //    _internalCts = CancellationTokenSource.CreateLinkedTokenSource(_sourceToCancelWhenDisconnectDetected.Token, _tokenToMonitor);
        //}

        //internal async Task<bool> Execute<T>(Func<T, CancellationToken, Task> guardFromClientDisconnect, T state)
        //{
        //    try
        //    {
        //        return await ExecuteWithTimeout(guardFromClientDisconnect, state);
        //    }
        //    catch (Exception exc) when (ClientDisconnected(exc, _tokenToMonitor))
        //    {
        //        return true;
        //    }
        //}

        //private async Task<bool> ExecuteWithTimeout<T>(Func<T, CancellationToken, Task> guardFromClientDisconnect, T state)
        //{
        //    _internalCts.CancelAfter(_timeout);
        //    await guardFromClientDisconnect(state, _internalCts.Token);
        //    _internalCts.TryReset();

        //    _internalCts.Token.ThrowIfCancellationRequested();

        //    return false;
        //}

        ///// <summary>
        ///// Returns true if the client disconnected, otherwise false.
        ///// </summary>
        ///// <param name="exception">The exception retrieved from the operation that might have been caused by a client disconnect</param>
        ///// <param name="cancellationToken">The client's request cancellation token</param>
        ///// <returns>True if the client disconnected, otherwise false</returns>
        //private static bool ClientDisconnected(Exception exception, CancellationToken cancellationToken)
        //{
        //    if (cancellationToken.IsCancellationRequested)
        //    {
        //        return true;
        //    }

        //    var exceptionFullName = exception.GetType().FullName;

        //    // IsCancellationRequested is false when connecting directly to Kestrel in ASP.NET Core 1.1 (on netcoreapp1_1). 
        //    // Instead the exception below is thrown.
        //    if (exceptionFullName == "Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException")
        //    {
        //        return true;
        //    }

        //    // IsCancellationRequested is false in some scenarios when connecting directly to Kestrel in ASP.NET Core 3.1 (on netcoreapp3_1).
        //    if (exceptionFullName == "Microsoft.AspNetCore.Connections.ConnectionResetException")
        //    {
        //        return true;
        //    }

        //    return false;
        //}
    }
}
