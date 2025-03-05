using System.Collections.Generic;
using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;

namespace tusdotnet.FileLocks
{
    /// <inheritdoc />
    public sealed class InMemoryFileLock : ITusFileLock
    {
        private readonly string _fileId;
        private static readonly HashSet<string> LockedFiles = new HashSet<string>();
        private bool _hasLock;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="fileId">The file id to try to lock.</param>
        public InMemoryFileLock(string fileId)
        {
            _fileId = fileId;
            _hasLock = false;
        }

        /// <inheritdoc />
        public Task<bool> Lock()
        {
            if (_hasLock)
            {
                return Task.FromResult(true);
            }

            lock (LockedFiles)
            {
                if (!LockedFiles.Contains(_fileId))
                {
                    LockedFiles.Add(_fileId);
                    _hasLock = true;
                }
                else
                {
                    _hasLock = false;
                }
            }

            return Task.FromResult(_hasLock);
        }

        /// <inheritdoc />
        public Task ReleaseIfHeld()
        {
            if (!_hasLock)
            {
                return TaskHelper.Completed;
            }

            lock (LockedFiles)
            {
                LockedFiles.Remove(_fileId);
            }

            return TaskHelper.Completed;
        }
    }
}
