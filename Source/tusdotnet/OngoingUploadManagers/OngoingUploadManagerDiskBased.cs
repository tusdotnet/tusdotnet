using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace tusdotnet.OngoingUploadManagers
{
    /// <summary>
    /// Disk-based upload manager suitable for multi-server deployments with shared storage.
    /// </summary>
    public sealed class OngoingUploadManagerDiskBased : IOngoingUploadManager
    {
        private const string ActiveSuffix = ".uploading";
        private const string RevokeSuffix = ".revoke";

        private readonly string _sharedPath;
        private readonly TimeSpan _cancelWaitTimeout;
        private readonly TimeSpan _pollInterval;

        /// <summary>
        /// Creates a new disk-based upload manager.
        /// </summary>
        /// <param name="sharedPath">Shared directory used for synchronization files.</param>
        public OngoingUploadManagerDiskBased(string sharedPath)
            : this(sharedPath, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(200)) { }

        /// <summary>
        /// Creates a new disk-based upload manager.
        /// </summary>
        /// <param name="sharedPath">Shared directory used for synchronization files.</param>
        /// <param name="cancelWaitTimeout">Max wait time for an older request to release.</param>
        /// <param name="pollInterval">Polling interval used when waiting for synchronization file changes.</param>
        public OngoingUploadManagerDiskBased(
            string sharedPath,
            TimeSpan cancelWaitTimeout,
            TimeSpan pollInterval
        )
        {
            _sharedPath = sharedPath;
            _cancelWaitTimeout = cancelWaitTimeout;
            _pollInterval = pollInterval;

            if (!Directory.Exists(_sharedPath))
            {
                Directory.CreateDirectory(_sharedPath);
            }
        }

        /// <inheritdoc />
        public async Task<IOngoingUpload> AcquireAsync(string uploadId)
        {
            var activePath = GetActivePath(uploadId);
            var revokePath = GetRevokePath(uploadId);
            var current = default(ActiveUpload);

            while (true)
            {
                current = TryCreateActiveUpload(uploadId, activePath, revokePath);
                if (current is not null)
                {
                    return current;
                }

                SafeWriteEmptyFile(revokePath);

                var previousStopped = await WaitUntilFileDeleted(activePath, _cancelWaitTimeout);
                if (!previousStopped)
                {
                    OngoingUploadManagerThrowHelper.ThrowTimeoutException(uploadId);
                }
            }
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(IOngoingUpload upload)
        {
            if (upload is ActiveUpload activeUpload)
            {
                await activeUpload.ReleaseAsync();
            }
        }

        private ActiveUpload TryCreateActiveUpload(string uploadId, string activePath, string revokePath)
        {
            try
            {
                var activeHandle = new FileStream(activePath, FileMode.CreateNew, FileAccess.ReadWrite);
                activeHandle.SetLength(0);

                SafeDelete(revokePath);

                return new ActiveUpload(uploadId, activePath, revokePath, activeHandle, _pollInterval);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private async Task<bool> WaitUntilFileDeleted(string path, TimeSpan timeout)
        {
            var started = DateTime.UtcNow;

            while (File.Exists(path))
            {
                if (DateTime.UtcNow - started >= timeout)
                {
                    return false;
                }

                await Task.Delay(_pollInterval);
            }

            return true;
        }

        private string GetActivePath(string uploadId)
        {
            return Path.Combine(_sharedPath, EncodeUploadId(uploadId) + ActiveSuffix);
        }

        private string GetRevokePath(string uploadId)
        {
            return Path.Combine(_sharedPath, EncodeUploadId(uploadId) + RevokeSuffix);
        }

        private static string EncodeUploadId(string uploadId)
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(uploadId));
            return encoded.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static void SafeWriteEmptyFile(string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fs.SetLength(0);
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        private sealed class ActiveUpload : IOngoingUpload
        {
            private readonly string _activePath;
            private readonly string _revokePath;
            private readonly FileStream _activeHandle;
            private readonly CancellationTokenSource _cancellation;
            private readonly CancellationTokenSource _monitorStop;
            private readonly Task _monitorTask;
            private int _released;

            internal ActiveUpload(
                string uploadId,
                string activePath,
                string revokePath,
                FileStream activeHandle,
                TimeSpan pollInterval
            )
            {
                UploadId = uploadId;
                _activePath = activePath;
                _revokePath = revokePath;
                _activeHandle = activeHandle;
                _cancellation = new CancellationTokenSource();
                _monitorStop = new CancellationTokenSource();
                _monitorTask = StartRevokeMonitor(pollInterval);
            }

            public string UploadId { get; }

            public CancellationToken CancellationToken => _cancellation.Token;

            public bool IsCancellationRequested => _cancellation.IsCancellationRequested;

            internal async Task ReleaseAsync()
            {
                if (Interlocked.Exchange(ref _released, 1) != 0)
                {
                    return;
                }

                _monitorStop.Cancel();
                _cancellation.Cancel();

                try
                {
                    await _monitorTask;
                }
                catch
                {
                    // Ignore monitor shutdown failures.
                }

                _activeHandle.Dispose();

                SafeDelete(_activePath);
                SafeDelete(_revokePath);

                _monitorStop.Dispose();
                _cancellation.Dispose();
            }

            private Task StartRevokeMonitor(TimeSpan pollInterval)
            {
                return Task.Run(async () =>
                {
                    while (!_monitorStop.IsCancellationRequested)
                    {
                        if (File.Exists(_revokePath))
                        {
                            _cancellation.Cancel();
                            break;
                        }

                        try
                        {
                            await Task.Delay(pollInterval, _monitorStop.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });
            }
        }
    }
}
