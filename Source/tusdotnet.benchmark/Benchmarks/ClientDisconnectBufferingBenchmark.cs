using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models.PipeReaders;
using tusdotnet.Helpers;
using tusdotnet.Stores;

namespace tusdotnet.benchmark.Benchmarks
{
    /// <summary>
    /// Measures the wall-clock time saved by retaining buffered data on client disconnect.
    ///
    /// Scenario: upload a 10 MB file in 49 KB chunks (just below the 50 KB write-buffer
    /// threshold so TusDiskStore never flushes mid-chunk). Every other request disconnects
    /// after sending one chunk, simulating a ~49 KB/s client on an unreliable connection.
    ///
    /// Old (ClientDisconnectGuardedPipeReaderOld):
    ///   Disconnect → empty buffer returned → TusDiskStore writes nothing → client retries
    ///   from same offset → wastes one full round-trip (1 second) per disconnect.
    ///
    /// New (ClientDisconnectGuardedPipeReader):
    ///   Disconnect → buffered 49 KB returned → TusDiskStore writes it → client retries
    ///   from a higher offset → no wasted round-trips.
    /// </summary>
    public class ClientDisconnectBufferingBenchmark
    {
        private const int ChunkSize = 49 * 1024;
        private static readonly long TotalFileSize = 10 * 1024 * 1024;

        private readonly TimeSpan _chunkDelay;

        public ClientDisconnectBufferingBenchmark(int chunkDelayMs = 1000)
        {
            _chunkDelay = TimeSpan.FromMilliseconds(chunkDelayMs);
        }

        private string _uploadDir = null!;
        private TusDiskStore _store = null!;

        public void Setup()
        {
            _uploadDir = Path.Combine(Path.GetTempPath(), "tusdotnet-bench-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_uploadDir);
            _store = new TusDiskStore(_uploadDir);
        }

        public void Cleanup()
        {
            if (Directory.Exists(_uploadDir))
                Directory.Delete(_uploadDir, recursive: true);
        }

        public async Task<(int requests, long bytesRetried)> UploadWithOldReaderDebug()
        {
            var fileId = await _store.CreateFileAsync(TotalFileSize, null, CancellationToken.None);
            return await UploadWithRetries(fileId, useNewReader: false);
        }

        public async Task<(int requests, long bytesRetried)> UploadWithNewReaderDebug()
        {
            var fileId = await _store.CreateFileAsync(TotalFileSize, null, CancellationToken.None);
            return await UploadWithRetries(fileId, useNewReader: true);
        }

        private async Task<(int requests, long bytesRetried)> UploadWithRetries(string fileId, bool useNewReader)
        {
            var requestIndex = 0;
            var totalBytesRetried = 0L;

            while (true)
            {
                var currentOffset = await _store.GetUploadOffsetAsync(fileId, CancellationToken.None);
                if (currentOffset >= TotalFileSize)
                    break;

                var shouldDisconnect = requestIndex % 2 == 0;
                requestIndex++;

                var disconnectCts = new CancellationTokenSource();
                var guard = new ClientDisconnectGuardWithTimeout(TimeSpan.FromSeconds(30), disconnectCts.Token);
                var simulatedReader = new SimulatedChunkedPipeReader(
                    chunkSize: (int)Math.Min(ChunkSize, TotalFileSize - currentOffset),
                    disconnect: shouldDisconnect,
                    disconnectCts: disconnectCts,
                    chunkDelay: _chunkDelay
                );

                PipeReader guardedReader = useNewReader
                    ? new ClientDisconnectGuardedPipeReader(simulatedReader, guard)
                    : (PipeReader)new ClientDisconnectGuardedPipeReaderOld(simulatedReader, guard);

                try
                {
                    await _store.AppendDataAsync(fileId, guardedReader, disconnectCts.Token);
                }
                catch (Exception) when (disconnectCts.IsCancellationRequested) { /* expected */ }
                disconnectCts.Dispose();

                var newOffset = await _store.GetUploadOffsetAsync(fileId, CancellationToken.None);
                if (shouldDisconnect && newOffset == currentOffset)
                    totalBytesRetried += ChunkSize;

                if (requestIndex > 500)
                    break;
            }

            return (requestIndex, totalBytesRetried);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Delivers exactly one chunk then either disconnects (faults) or completes
    // normally. Uses a simple TaskCompletionSource handshake to avoid any pipe
    // backpressure or race conditions.
    // ─────────────────────────────────────────────────────────────────────────────
    internal sealed class SimulatedChunkedPipeReader : PipeReader
    {
        private readonly byte[] _data;
        private readonly bool _disconnect;
        private readonly CancellationTokenSource _disconnectCts;
        private readonly TimeSpan _chunkDelay;

        // State machine: 0 = not yet read, 1 = data returned (awaiting AdvanceTo), 2 = done
        private int _state = 0;
        private readonly TaskCompletionSource _advancedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SimulatedChunkedPipeReader(int chunkSize, bool disconnect, CancellationTokenSource disconnectCts, TimeSpan chunkDelay)
        {
            _data = new byte[chunkSize];
            _data.AsSpan().Fill(0xAB);
            _disconnect = disconnect;
            _disconnectCts = disconnectCts;
            _chunkDelay = chunkDelay;
        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_state == 0)
            {
                // First read: deliver the chunk after the delay
                if (_chunkDelay > TimeSpan.Zero)
                    await Task.Delay(_chunkDelay);

                _state = 1;

                if (_disconnect)
                {
                    // Simulate disconnect: cancel guard token, return buffered data with isCanceled.
                    // We do this by returning the data normally here — TusDiskStore will call
                    // AdvanceTo(start, end) since it's < 50 KB, then ReadAsync again.
                    // On the second ReadAsync we simulate the actual disconnect.
                    return new ReadResult(new System.Buffers.ReadOnlySequence<byte>(_data), isCanceled: false, isCompleted: false);
                }
                else
                {
                    return new ReadResult(new System.Buffers.ReadOnlySequence<byte>(_data), isCanceled: false, isCompleted: true);
                }
            }
            else if (_state == 1 && _disconnect)
            {
                // Second read after disconnect chunk: simulate the connection reset
                _disconnectCts.Cancel();
                throw new IOException("Connection reset by peer");
            }

            // Completed
            return new ReadResult(default, isCanceled: false, isCompleted: true);
        }

        public override void AdvanceTo(SequencePosition consumed) { }
        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) { }
        public override void CancelPendingRead() { }
        public override void Complete(Exception exception = null) { }
        public override bool TryRead(out ReadResult result) { result = default; return false; }
    }
}
