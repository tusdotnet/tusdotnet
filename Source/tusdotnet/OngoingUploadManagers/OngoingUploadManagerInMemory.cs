using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;

namespace tusdotnet.OngoingUploadManagers
{
    /// <summary>
    /// In-memory upload manager suitable for single-server deployments.
    /// </summary>
    public sealed class OngoingUploadManagerInMemory : IOngoingUploadManager
    {
        /// <summary>
        /// Shared singleton instance.
        /// </summary>
        public static IOngoingUploadManager Instance { get; } = new OngoingUploadManagerInMemory();

        private readonly object _gate = new object();
        private readonly Dictionary<string, ActiveUpload> _activeByUploadId =
            new Dictionary<string, ActiveUpload>();
        private readonly TimeSpan _cancelWaitTimeout;

        /// <summary>
        /// Creates a new in-memory upload manager.
        /// </summary>
        public OngoingUploadManagerInMemory()
            : this(TimeSpan.FromSeconds(30)) { }

        /// <summary>
        /// Creates a new in-memory upload manager with a custom preemption timeout.
        /// </summary>
        /// <param name="cancelWaitTimeout">Max wait time for an older request to release.</param>
        public OngoingUploadManagerInMemory(TimeSpan cancelWaitTimeout)
        {
            _cancelWaitTimeout = cancelWaitTimeout;
        }

        /// <inheritdoc />
        public async Task<IOngoingUpload> AcquireAsync(string uploadId)
        {
            var current = new ActiveUpload(uploadId);
            ActiveUpload previous;

            lock (_gate)
            {
                _activeByUploadId.TryGetValue(uploadId, out previous);
                _activeByUploadId[uploadId] = current;
            }

            if (previous is null)
            {
                return current;
            }

            previous.Cancel();

            var completed = await WaitForCompletion(
                previous.ReleaseCompleted.Task,
                _cancelWaitTimeout
            );
            if (completed)
            {
                return current;
            }

            current.Cancel();
            current.Dispose();

            lock (_gate)
            {
                // Restore the previous upload as the active owner since it never stopped.
                // Without this, the dictionary would be empty and a subsequent request
                // could acquire concurrently with the still-running previous request.
                // Only restore if previous hasn't completed in the meantime (race between
                // timeout and release completing simultaneously).
                if (
                    _activeByUploadId.TryGetValue(uploadId, out var active)
                    && ReferenceEquals(active, current)
                    && !previous.ReleaseCompleted.Task.IsCompleted
                )
                {
                    _activeByUploadId[uploadId] = previous;
                }
            }

            OngoingUploadManagerThrowHelper.ThrowTimeoutException(uploadId);
            return null;
        }

        /// <inheritdoc />
        public Task ReleaseAsync(IOngoingUpload upload)
        {
            if (upload is not ActiveUpload activeUpload)
            {
                return TaskHelper.Completed;
            }

            lock (_gate)
            {
                if (
                    _activeByUploadId.TryGetValue(activeUpload.UploadId, out var active)
                    && ReferenceEquals(active, activeUpload)
                )
                {
                    _activeByUploadId.Remove(activeUpload.UploadId);
                }
            }

            activeUpload.Dispose();
            activeUpload.ReleaseCompleted.TrySetResult(true);

            return TaskHelper.Completed;
        }

        private static async Task<bool> WaitForCompletion(Task completion, TimeSpan timeout)
        {
            if (completion.IsCompleted)
            {
                return true;
            }

            var timeoutTask = Task.Delay(timeout);
            var first = await Task.WhenAny(completion, timeoutTask);
            return ReferenceEquals(first, completion);
        }

        private sealed class ActiveUpload : IOngoingUpload
        {
            private int _disposed;

            internal ActiveUpload(string uploadId)
            {
                UploadId = uploadId;
                Cancellation = new CancellationTokenSource();
                ReleaseCompleted = new TaskCompletionSource<bool>();
            }

            public string UploadId { get; }

            public CancellationToken CancellationToken => Cancellation.Token;

            public bool IsCancellationRequested => Cancellation.IsCancellationRequested;

            internal CancellationTokenSource Cancellation { get; }

            internal TaskCompletionSource<bool> ReleaseCompleted { get; }

            internal void Cancel()
            {
                Cancellation.Cancel();
            }

            internal void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                Cancellation.Dispose();
            }
        }
    }
}
