#if netstandard && !NET6_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    /*
    * "Subsequent calls to CancelAfter will reset the delay for this CancellationTokenSource, if it has not been canceled already."
    * https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelafter?view=netcore-3.1#system-threading-cancellationtokensource-cancelafter(system-timespan)
    *
    * Using Timeout.Infinite will remove the timer from the timer queue, making it a no-op:
    * https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Threading/CancellationTokenSource.cs,418
    * https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Threading/Timer.cs,552
    */

    internal partial class ClientDisconnectGuardWithTimeout
    {
#if pipelines

        private void ExecuteWithTimeout(Action guardFromClientDisconnect)
        {
            _cts.CancelAfter(_executionTimeout);

            guardFromClientDisconnect();

            _cts.CancelAfter(Timeout.Infinite);
        }
#endif

        private async Task<T> ExecuteWithTimeout<T>(Func<Task<T>> guardFromClientDisconnect)
        {
            _cts.CancelAfter(_executionTimeout);

            var res = await guardFromClientDisconnect();

            _cts.CancelAfter(Timeout.Infinite);

            return res;
        }
    }
}

#endif
