using System;
using System.IO;
using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;

namespace tusdotnet.FileLocks
{
    /// <inheritdoc />
    public sealed class DiskFileLock : ITusFileLock
    {
        private bool _hasLock;
        private readonly string _fileLockDiskLocation;

        /// <inheritdoc />
        public DiskFileLock(string lockFolderLocation, string fileId)
        {
            // Sanitize fileId by extracting only the filename portion, removing any path components.
            // Normalize backslashes to forward slashes to ensure cross-platform compatibility,
            // since Path.GetFileName only treats \ as a separator on Windows.
            var normalizedFileId = fileId?.Replace('\\', '/') ?? "";
            var safeFileId = Path.GetFileName(normalizedFileId);

            // If sanitization results in an empty string, _fileLockDiskLocation remains null.
            if (!string.IsNullOrEmpty(safeFileId))
            {
                _fileLockDiskLocation = Path.Combine(lockFolderLocation, safeFileId + ".lock");
            }
        }

        /// <inheritdoc />
        public Task<bool> Lock()
        {
            if (_fileLockDiskLocation == null)
            {
                return Task.FromResult(false);
            }

            if (_hasLock)
            {
                return Task.FromResult(true);
            }

            try
            {
                new FileStream(_fileLockDiskLocation, FileMode.CreateNew).Dispose();
                _hasLock = true;
            }
            catch (Exception)
            {
                _hasLock = false;
            }

            return Task.FromResult(_hasLock);
        }

        /// <inheritdoc />
        public Task ReleaseIfHeld()
        {
            if (_hasLock && _fileLockDiskLocation != null)
            {
                File.Delete(_fileLockDiskLocation);
            }
            return TaskHelper.Completed;
        }
    }
}
