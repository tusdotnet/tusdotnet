#if netfull

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal partial class ClientDisconnectGuardWithTimeout
    {
        private Task<T> ExecuteWithTimeout<T>(Func<Task<T>> guardFromClientDisconnect)
        {
            // NOTE: Do not await here to hide ExecuteWithTimeout from stacktraces.
            return guardFromClientDisconnect();
        }
    }
}

#endif
