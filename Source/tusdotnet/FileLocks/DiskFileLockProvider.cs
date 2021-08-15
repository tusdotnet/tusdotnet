using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.FileLocks
{
    /// <summary>
    /// Provides locks for files on disk.
    /// </summary>
    public sealed class DiskFileLockProvider : ITusFileLockProvider
    {
        private readonly string _lockFolderLocation;

        /// <summary>
        /// Creates a new DiskFileLockProvider
        /// </summary>
        /// <param name="lockFolderLocation">The folder where to save lock files</param>
        public DiskFileLockProvider(string lockFolderLocation)
        {
            _lockFolderLocation = lockFolderLocation;
        }

        /// <inheritdoc />
        public Task<ITusFileLock> AquireLock(string fileId)
        {
            return Task.FromResult<ITusFileLock>(new DiskFileLock(_lockFolderLocation, fileId));
        }
    }
}
