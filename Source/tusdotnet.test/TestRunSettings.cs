using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.test
{
    internal static class TestRunSettings
    {
        internal static bool SupportsEvents = false;

        internal static bool UseHandlerPattern = true;

        [ModuleInitializer]
        internal static void InitializeSettings()
        {
            // TODO: Do stuff, read env variables and set properties accordingly.
        }
    }
}
