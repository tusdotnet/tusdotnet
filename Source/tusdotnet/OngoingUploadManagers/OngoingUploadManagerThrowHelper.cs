using System;
using tusdotnet.Exceptions;

namespace tusdotnet.OngoingUploadManagers
{
    internal static class OngoingUploadManagerThrowHelper
    {
        internal static void ThrowTimeoutException(string uploadId)
        {
            throw new LockAcquisitionTimeoutException(uploadId);
        }
    }
}
