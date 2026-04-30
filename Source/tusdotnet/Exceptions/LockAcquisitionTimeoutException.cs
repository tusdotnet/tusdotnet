using System;

namespace tusdotnet.Exceptions
{
    /// <summary>
    /// Exception thrown when a lock cannot be acquired within the configured timeout
    /// because a previous request did not stop in time.
    /// </summary>
    public sealed class LockAcquisitionTimeoutException : Exception
    {
        internal LockAcquisitionTimeoutException(string uploadId)
            : base($"Timeout when waiting for previous request to stop for upload id '{uploadId}'.")
        {
        }
    }
}
