using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    /// <summary>
    /// Helper for handling client disconnects when executing code.
    /// </summary>
    public static class ClientDisconnectGuard
    {
        /// <summary>
        /// Execute the provided func and return true if the client disconnected during the call to func.
        /// </summary>
        /// <param name="guardFromClientDisconnect">The func to execute</param>
        /// <param name="requestCancellationToken">The cancellation token of the request to monitor for disconnects</param>
        /// <returns>True if the client disconnected, otherwise false</returns>
        public static async Task<bool> ExecuteAsync(Func<Task> guardFromClientDisconnect, CancellationToken requestCancellationToken)
        {
            try
            {
                await guardFromClientDisconnect();
                return false;
            }
            catch (Exception exc) when (ClientDisconnected(exc, requestCancellationToken))
            {
                return true;
            }
        }

        /// <summary>
        /// Execute the provided action and return true if the client disconnected during the call to action.
        /// </summary>
        /// <param name="guardFromClientDisconnect">The action to execute</param>
        /// <param name="requestCancellationToken">The cancellation token of the request to monitor for disconnects</param>
        /// <returns>True if the client disconnected, otherwise false</returns>
        public static bool Execute(Action guardFromClientDisconnect, CancellationToken requestCancellationToken)
        {
            try
            {
                guardFromClientDisconnect();
                return false;
            }
            catch (Exception exc) when (ClientDisconnected(exc, requestCancellationToken))
            {
                return true;
            }
        }

        internal static async Task<ClientDisconnectGuardReadStreamAsyncResult> ReadStreamAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, offset, count, cancellationToken);
                return new ClientDisconnectGuardReadStreamAsyncResult(false, bytesRead);
            }
            catch (Exception exc) when (ClientDisconnected(exc, cancellationToken))
            {
                return new ClientDisconnectGuardReadStreamAsyncResult(true, 0);
            }
        }

        /// <summary>
        /// Returns true if the client disconnected, otherwise false.
        /// </summary>
        /// <param name="exception">The exception retrieved from the operation that might have been caused by a client disconnect</param>
        /// <param name="cancellationToken">The client's request cancellation token</param>
        /// <returns>True if the client disconnected, otherwise false</returns>
        internal static bool ClientDisconnected(Exception exception, CancellationToken cancellationToken)
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
    }
}
