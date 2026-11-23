using System.Threading;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// A file lock that can be lost in the middle of a request, e.g. a distributed lock backed by an expiring
    /// lease or by a database connection.
    /// </summary>
    /// <remarks>
    /// Another request may acquire a lost lock while this one still writes. Once the lock is lost, tusdotnet stops
    /// reading the request body: the next read, or a read in progress, throws and the request answers 409 Conflict.
    /// This cannot undo data the store wrote before the lock was lost. Only the store can reject that write: it
    /// checks <c>GetFileLockLostToken()</c> in <c>AppendDataAsync</c>, or, more reliably, uses a conditional write
    /// against the storage.
    /// tusdotnet still calls <see cref="ITusFileLock.ReleaseIfHeld"/> after the lock is lost. Release only a lock
    /// this instance still owns (e.g. compare the lease owner); otherwise the release removes the lock that another
    /// request now holds.
    /// </remarks>
    public interface ITusLeasedFileLock : ITusFileLock
    {
        /// <summary>
        /// Cancelled when this instance loses the lock it acquired in <see cref="ITusFileLock.Lock"/>.
        /// Return <see cref="CancellationToken.None"/> if the lock is not held.
        /// </summary>
        CancellationToken LockLostToken { get; }
    }
}
