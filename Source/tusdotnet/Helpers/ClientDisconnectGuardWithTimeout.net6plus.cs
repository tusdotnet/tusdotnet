#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}

#endif
