using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class OngoingUploadManagerInMemory : IOngoingUploadManager
    {
        private readonly Dictionary<string, CancellationTokenSource> _cancelCts;
        private readonly Dictionary<string, CancellationTokenSource> _cancelNotifyCts;

        private const int CANCEL_WAIT_TIMEOUT_IN_MS = 30_000;

        public OngoingUploadManagerInMemory()
        {
            _cancelCts = new();
            _cancelNotifyCts = new();
        }

        public Task<CancellationToken> StartUpload(string uploadToken)
        {
            var cts = new CancellationTokenSource();
            _cancelCts.Add(uploadToken, cts);

            return Task.FromResult(cts.Token);
        }

        public Task FinishUpload(string uploadToken)
        {
            if (_cancelCts.TryGetValue(uploadToken, out CancellationTokenSource cts))
            {
                cts.Dispose();
                _cancelCts.Remove(uploadToken);
            }

            return Task.CompletedTask;
        }

        public async Task CancelOtherUploads(string uploadToken)
        {
            if (!_cancelCts.TryGetValue(uploadToken, out var cancelOtherRequestCts))
                return;

            var cancelNotify = new CancellationTokenSource();
            _cancelNotifyCts.Add(uploadToken, cancelNotify);

            cancelOtherRequestCts.Cancel();

            try
            {
                await Task.Delay(CANCEL_WAIT_TIMEOUT_IN_MS, cancelNotify.Token);
            }
            catch (Exception)
            {
                // Cancelled
            }

            _cancelNotifyCts.Remove(uploadToken);

            if (!cancelNotify.IsCancellationRequested)
                UploadManagerThrowHelper.ThrowTimeoutException();
        }

        public Task NotifyCancelComplete(string uploadToken)
        {
            if (_cancelNotifyCts.TryGetValue(uploadToken, out var cts))
                cts.Cancel();

            return Task.CompletedTask;
        }
    }
}
