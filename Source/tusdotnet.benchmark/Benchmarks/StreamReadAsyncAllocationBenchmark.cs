using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Streams;

namespace tusdotnet.benchmark.Benchmarks
{
    /// <summary>
    /// Measures allocations and throughput for a hot-path Stream read loop:
    ///   ReadAsync → repeat (N chunks)
    ///
    /// Compares two implementations:
    ///   Current   — ClientDisconnectGuardedReadOnlyStream   (Func&lt;Task&lt;T&gt;&gt; + closures)
    ///   Optimized — ClientDisconnectGuardedReadOnlyStreamOptimized (static lambdas, no closure)
    ///
    /// Note: Stream.ReadAsync always allocates a Task&lt;int&gt;, so the saving is smaller than
    /// the PipeReader case — only the two delegate/closure wrappers are eliminated.
    /// </summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class StreamReadAsyncAllocationBenchmark
    {
        [Params(10, 100)]
        public int Chunks { get; set; }

        // ─────────────────────────────────────────────────────────────────────────
        // Current (closure-based)

        [Benchmark(Baseline = true, Description = "Current (Func<Task<T>> closure)")]
        public async Task<long> CurrentStream()
        {
            var (stream, guard, cts) = BuildStack(Chunks);
            var guarded = new ClientDisconnectGuardedReadOnlyStream(stream, guard);
            try { return await DrainStream(guarded, Chunks); }
            finally { cts.Dispose(); }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Optimized (static lambda)

        [Benchmark(Description = "Optimized (static lambda, no closure)")]
        public async Task<long> OptimizedStream()
        {
            var (stream, guard, cts) = BuildStack(Chunks);
            var guarded = new ClientDisconnectGuardedReadOnlyStreamOptimized(stream, guard);
            try { return await DrainStream(guarded, Chunks); }
            finally { cts.Dispose(); }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers

        private static (SyntheticStream stream, ClientDisconnectGuardWithTimeout guard, CancellationTokenSource cts) BuildStack(int chunks)
        {
            var cts = new CancellationTokenSource();
            var guard = new ClientDisconnectGuardWithTimeout(TimeSpan.FromSeconds(30), cts.Token);
            var stream = new SyntheticStream(chunks);
            return (stream, guard, cts);
        }

        private static async Task<long> DrainStream(Stream stream, int chunks)
        {
            var buffer = new byte[4 * 1024];
            long total = 0;
            for (int i = 0; i < chunks; i++)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
                total += read;
                if (read == 0) break;
            }
            return total;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // A synchronous-completing Stream that returns a fixed 4 KB chunk on every read.
    // No real I/O so we isolate the delegate/closure allocation overhead.
    // ─────────────────────────────────────────────────────────────────────────────
    internal sealed class SyntheticStream : Stream
    {
        private const int ChunkBytes = 4 * 1024;
        private readonly byte[] _chunk = new byte[ChunkBytes];
        private readonly int _totalChunks;
        private int _chunksRead;

        public SyntheticStream(int totalChunks)
        {
            _totalChunks = totalChunks;
            _chunk.AsSpan().Fill(0xAB);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_chunksRead >= _totalChunks)
                return Task.FromResult(0);

            var bytes = Math.Min(count, ChunkBytes);
            _chunk.AsSpan(0, bytes).CopyTo(buffer.AsSpan(offset));
            _chunksRead++;
            return Task.FromResult(bytes);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
