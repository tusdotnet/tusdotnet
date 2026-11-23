namespace tusdotnet.Models
{
    /// <summary>
    /// Thrown by the lock-lost guards when the request body is read after the file lock was lost.
    /// It derives from <see cref="TusUploadConflictException"/>, so the runner answers 409 Conflict.
    /// It is not an <see cref="System.OperationCanceledException"/>: stores treat a cancelled read as a client
    /// disconnect and save the data received so far.
    /// </summary>
    internal sealed class FileLockLostException : TusUploadConflictException
    {
        internal const string DefaultMessage =
            "The file lock was lost while the file was being updated. Please try again";

        internal FileLockLostException()
            : base(DefaultMessage) { }
    }
}
