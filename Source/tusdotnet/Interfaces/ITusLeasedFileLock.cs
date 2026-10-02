using System.Threading;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// A file lock that can be lost in the middle of a request, e.g. a distributed lock backed by an expiring
    /// lease or by a database connection.
    /// </summary>
    /// <remarks>
    /// A lost lock does not stop the request, and another request may acquire the lock while this one still
    /// writes. Only the store can reject the stale write: it reads the token with <c>GetFileLockLostToken()</c>
    /// in <c>AppendDataAsync</c>, or better, uses a conditional write against the storage.
    /// tusdotnet still calls <see cref="ITusFileLock.ReleaseIfHeld"/> after the lock is lost, so release only a lock
    /// this instance still owns (e.g. by comparing the lease owner), or it removes the lock another request now holds.
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
