using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using tusdotnet.Helpers;
using tusdotnet.Models.PipeReaders;

namespace tusdotnet.benchmark.Benchmarks
{
    /// <summary>
    /// Measures allocations and throughput for a hot-path read loop:
    ///   ReadAsync → AdvanceTo → repeat (N chunks)
    ///
    /// Compares three implementations:
    ///   Old   — ClientDisconnectGuardedPipeReaderOld   (Func&lt;Task&lt;T&gt;&gt; + closures)
    ///   Current — ClientDisconnectGuardedPipeReader    (same closure pattern, adds _unconsumedBuffer)
    ///   Optimized — ClientDisconnectGuardedPipeReaderOptimized (static lambdas, Option A)
    /// </summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class ReadAsyncAllocationBenchmark
    {
        // Number of chunks to read per benchmark iteration
        [Params(10, 100)]
        public int Chunks { get; set; }

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        // ─────────────────────────────────────────────────────────────────────────
        // Old reader

        [Benchmark(Baseline = true, Description = "Old (Func<Task<T>> closure)")]
        public async Task<long> OldReader()
        {
            var (reader, guard, cts) = BuildPipeStack(Chunks);
            var guardedReader = new ClientDisconnectGuardedPipeReaderOld(reader, guard);
            try { return await DrainReader(guardedReader); }
            finally { cts.Dispose(); }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Current reader

        [Benchmark(Description = "Current (Func<Task<T>> closure + unconsumedBuffer)")]
        public async Task<long> CurrentReader()
        {
            var (reader, guard, cts) = BuildPipeStack(Chunks);
            var guardedReader = new ClientDisconnectGuardedPipeReader(reader, guard);
            try { return await DrainReader(guardedReader); }
            finally { cts.Dispose(); }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Optimized reader (Option A)

        [Benchmark(Description = "Optimized (static lambda, no closure)")]
        public async Task<long> OptimizedReader()
        {
            var (reader, guard, cts) = BuildPipeStack(Chunks);
            var guardedReader = new ClientDisconnectGuardedPipeReaderOptimized(reader, guard);
            try { return await DrainReader(guardedReader); }
            finally { cts.Dispose(); }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers

        private static (SyntheticPipeReader reader, ClientDisconnectGuardWithTimeout guard, CancellationTokenSource cts) BuildPipeStack(int chunks)
        {
            var cts = new CancellationTokenSource();
            var guard = new ClientDisconnectGuardWithTimeout(TimeSpan.FromSeconds(30), cts.Token);
            var reader = new SyntheticPipeReader(chunks);
            return (reader, guard, cts);
        }

        private static async Task<long> DrainReader(PipeReader reader)
        {
            long total = 0;
            while (true)
            {
                var result = await reader.ReadAsync(CancellationToken.None);
                var buffer = result.Buffer;
                total += buffer.Length;
                reader.AdvanceTo(buffer.End);
                if (result.IsCompleted || result.IsCanceled)
                    break;
            }
            return total;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // A purely synchronous PipeReader that returns N chunks of 4 KB then completes.
    // There is no I/O delay so we isolate allocation overhead.
    // ─────────────────────────────────────────────────────────────────────────────
    internal sealed class SyntheticPipeReader : PipeReader
    {
        private const int ChunkBytes = 4 * 1024;
        private readonly int _totalChunks;
        private readonly byte[] _chunk = new byte[ChunkBytes];
        private int _chunksRead = 0;

        public SyntheticPipeReader(int totalChunks)
        {
            _totalChunks = totalChunks;
            _chunk.AsSpan().Fill(0xAB);
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_chunksRead >= _totalChunks)
                return new ValueTask<ReadResult>(new ReadResult(default, isCanceled: false, isCompleted: true));

            _chunksRead++;
            var seq = new ReadOnlySequence<byte>(_chunk);
            var isLast = _chunksRead >= _totalChunks;
            return new ValueTask<ReadResult>(new ReadResult(seq, isCanceled: false, isCompleted: isLast));
        }

        public override void AdvanceTo(SequencePosition consumed) { }
        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) { }
        public override void CancelPendingRead() { }
        public override void Complete(Exception exception = null) { }
        public override bool TryRead(out ReadResult result) { result = default; return false; }
    }
}
