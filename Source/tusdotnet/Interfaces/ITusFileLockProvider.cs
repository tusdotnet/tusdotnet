using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    /// <summary>
    /// Interface to provide a file lock for a specific file
    /// </summary>
    public interface ITusFileLockProvider
    {
        /// <summary>
        /// Returns the lock to use when locking the specific file.
        /// </summary>
        /// <param name="fileId">The id of the file to lock</param>
        /// <returns>The lock to use when locking the specific file</returns>
        Task<ITusFileLock> AquireLock(string fileId);
    }
}
