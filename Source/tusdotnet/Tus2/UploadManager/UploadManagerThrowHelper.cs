using System;

namespace tusdotnet.Tus2
{
    internal static class UploadManagerThrowHelper
    {
        public static void ThrowTimeoutException() => throw new TimeoutException("Timeout when trying to cancel other uploads");
    }
}
