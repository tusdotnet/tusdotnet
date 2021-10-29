using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.test.Helpers
{
    internal class TestServerInMemoryFileLockProvider : ITusFileLockProvider
    {
        private readonly HashSet<string> LockedFiles = new();

        public Task<ITusFileLock> AquireLock(string fileId)
        {
            return Task.FromResult<ITusFileLock>(new TestServerInMemoryFileLock(fileId, this));
        }

        private class TestServerInMemoryFileLock : ITusFileLock
        {
            private readonly string _fileId;
            private readonly TestServerInMemoryFileLockProvider _provider;

            private bool _hasLock;

            public TestServerInMemoryFileLock(string fileId, TestServerInMemoryFileLockProvider provider)
            {
                _fileId = fileId;
                _provider = provider;
                _hasLock = false;
            }

            public Task<bool> Lock()
            {
                if (!_provider.LockedFiles.Contains(_fileId))
                {
                    lock (_provider.LockedFiles)
                    {
                        if (!_provider.LockedFiles.Contains(_fileId))
                        {
                            _provider.LockedFiles.Add(_fileId);
                            _hasLock = true;
                            return Task.FromResult(true);
                        }
                    }
                }

                return Task.FromResult(false);
            }

            public Task ReleaseIfHeld()
            {
                if (_hasLock)
                {
                    lock (_provider.LockedFiles)
                    {
                        _provider.LockedFiles.Remove(_fileId);
                    }
                }

                return Task.FromResult(true);
            }
        }
    }
}
