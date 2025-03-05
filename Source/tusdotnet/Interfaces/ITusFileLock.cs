using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Interface for locking file access
    /// </summary>
    public interface ITusFileLock
    {
        /// <summary>
        /// Lock the file. Returns true if the file was locked or false if the file was already locked by another call.
        /// </summary>
        /// <returns>True if the file was locked or false if the file was already locked by another call.</returns>
        public Task<bool> Lock();

        /// <summary>
        /// Release the lock if held. If not held by the caller, this method is a no op.
        /// </summary>
        public Task ReleaseIfHeld();
    }
}
