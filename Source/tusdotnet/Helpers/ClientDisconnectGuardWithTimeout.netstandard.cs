#if netstandard && !NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal partial class ClientDisconnectGuardWithTimeout
    {

#if pipelines

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

#endif

        private async Task<T> ExecuteWithTimeout<T>(Func<Task<T>> guardFromClientDisconnect)
        {
            var timeout = Task.Delay(_executionTimeout);
            var res = guardFromClientDisconnect();

            var t = await Task.WhenAny(res, timeout);
            if (t == timeout)
            {
                _cts.Cancel();
                return default;
            }

            return res.Result;
        }
    }
}

#endif