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

        private static bool ClientDisconnected(Exception exception, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return true;
            }

            // IsCancellationRequested is false when connecting directly to Kestrel in ASP.NET Core 1.1 (on netcoreapp1_1). 
            // Instead the exception below is thrown.
            return exception.GetType().FullName == "Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException";
        }
    }
}
