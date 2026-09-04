#if pipelines

using Shouldly;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.Models.PipeReaders;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class ClientDisconnectGuardedPipeReaderTests
    {
        // In production, Context.CancellationToken == ClientDisconnectGuard.GuardedToken
        // (see ContextAdapter.cs: CancellationToken => ClientDisconnectGuard.GuardedToken).
        // The store always calls ReadAsync(Context.CancellationToken), so the guarded token
        // is what gets passed to the backing PipeReader.
        //
        // On a real Kestrel disconnect, two things happen concurrently:
        //   1. HttpContext.RequestAborted is cancelled (sets IsCancellationRequested = true)
        //   2. Kestrel calls Writer.Complete(exception) to signal EOF on the transport
        //
        // Both are needed to trigger Pipe.CompletePipe(), which is what actually returns the
        // pooled memory segments back to the MemoryPool:
        //   https://source.dot.net/#System.IO.Pipelines/System/IO/Pipelines/Pipe.cs,983
        //
        // Tests therefore always do both: cts.Cancel() + Writer.CompleteAsync().
        private static (
            ClientDisconnectGuardedPipeReader guardedReader,
            CancellationTokenSource cts
        ) CreateGuardedReader(PipeReader backingReader)
        {
            var cts = new CancellationTokenSource();
            var guard = new ClientDisconnectGuardWithTimeout(TimeSpan.FromSeconds(60), cts.Token);
            var guardedReader = new ClientDisconnectGuardedPipeReader(backingReader, guard);
            return (guardedReader, cts);
        }

        [Fact]
        public async Task ReadAsync_Returns_Unconsumed_Buffer_On_Disconnect_After_AdvanceToStartEnd()
        {
            var pipe = new Pipe();
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var originalData = Encoding.UTF8.GetBytes("ABCDEF");
            await pipe.Writer.WriteAsync(originalData);

            // First read — get data but do NOT consume any of it (simulate examine-only pattern)
            var firstResult = await guardedReader.ReadAsync(cts.Token);
            firstResult.Buffer.Length.ShouldBe(originalData.Length);
            guardedReader.AdvanceTo(firstResult.Buffer.Start, firstResult.Buffer.End);

            // Simulate Kestrel disconnect: cancel the request token and complete the writer with
            // an exception. The guard detects disconnect via either a cancelled token or a
            // connection-reset exception thrown by the backing reader.
            cts.Cancel();
            await pipe.Writer.CompleteAsync(new System.IO.IOException("Connection reset by peer"));

            // Act
            var fallbackResult = await guardedReader.ReadAsync(cts.Token);

            // Assert
            fallbackResult.IsCanceled.ShouldBeTrue();
            fallbackResult.Buffer.ToArray().ShouldBe(originalData);
        }

        [Fact]
        public async Task ReadAsync_Fallback_Buffer_Excludes_Consumed_Bytes()
        {
            var pipe = new Pipe();
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var originalData = Encoding.UTF8.GetBytes("ABCDEFGH");
            await pipe.Writer.WriteAsync(originalData);

            // First read — consume 3 bytes, examine all
            var firstResult = await guardedReader.ReadAsync(cts.Token);
            guardedReader.AdvanceTo(firstResult.Buffer.GetPosition(3), firstResult.Buffer.End);

            // Simulate disconnect
            cts.Cancel();
            await pipe.Writer.CompleteAsync(new System.IO.IOException("Connection reset by peer"));

            // Act
            var fallbackResult = await guardedReader.ReadAsync(cts.Token);

            // Assert — only the 5 unconsumed bytes should be returned
            fallbackResult.IsCanceled.ShouldBeTrue();
            fallbackResult.Buffer.ToArray().ShouldBe(Encoding.UTF8.GetBytes("DEFGH"));
        }

        [Fact]
        public async Task ReadAsync_Does_Not_Expose_Corrupted_Data_With_Pool_Churn_When_Disconnected()
        {
            var poisoningPool = new PoisoningMemoryPool();
            var pipe = new Pipe(new PipeOptions(pool: poisoningPool));
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var originalData = Encoding.UTF8.GetBytes("ORIGINAL_DATA");
            await pipe.Writer.WriteAsync(originalData);

            // Read but do not consume anything (examine-only)
            var firstResult = await guardedReader.ReadAsync(cts.Token);
            guardedReader.AdvanceTo(firstResult.Buffer.Start, firstResult.Buffer.End);

            // Simulate disconnect — Writer.Complete() triggers Pipe.CompletePipe() once the
            // reader is also completed, which is what actually returns segments to the pool.
            // If our code incorrectly called Reader.Complete() here, the memory would be
            // returned immediately and the churn below would overwrite it with 0xDE.
            cts.Cancel();
            await pipe.Writer.CompleteAsync(new System.IO.IOException("Connection reset by peer"));

            // Get the fallback buffer, then churn the pool aggressively with many other
            // pipes using the same poisoning pool. If memory was returned prematurely, the
            // churn pipes will rent the same blocks and overwrite them with 0xDE.
            var fallbackResult = await guardedReader.ReadAsync(cts.Token);

            var churnTasks = Enumerable
                .Range(0, 20)
                .Select(async _ =>
                {
                    var churnPipe = new Pipe(new PipeOptions(pool: poisoningPool));
                    var payload = new byte[originalData.Length * 4];
                    Array.Fill(payload, (byte)0xFF);
                    await churnPipe.Writer.WriteAsync(payload);
                    var result = await churnPipe.Reader.ReadAsync();
                    churnPipe.Reader.AdvanceTo(result.Buffer.End);
                    await churnPipe.Reader.CompleteAsync();
                    await churnPipe.Writer.CompleteAsync();
                });
            await Task.WhenAll(churnTasks);

            var bufferArray = fallbackResult.Buffer.ToArray();

            bufferArray.ShouldBe(originalData);
            bufferArray.ShouldNotContain((byte)0xDE);
            bufferArray.ShouldNotContain((byte)0xFF);
        }

        [Fact]
        public async Task Parallel_Flows_Do_Not_Corrupt_Fallback_Buffers()
        {
            // Each flow is independent (separate PipeReader per flow — PipeReader is not thread safe).
            // We verify that N concurrent flows each get exactly their own payload back after disconnect.
            const int flowCount = 10;

            var tasks = Enumerable
                .Range(0, flowCount)
                .Select(async i =>
                {
                    var poisoningPool = new PoisoningMemoryPool();
                    var pipe = new Pipe(new PipeOptions(pool: poisoningPool));
                    var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

                    var expectedData = Enumerable.Range(0, 16).Select(_ => (byte)(i + 1)).ToArray();
                    await pipe.Writer.WriteAsync(expectedData);

                    // Read, examine-only
                    var firstResult = await guardedReader.ReadAsync(cts.Token);
                    guardedReader.AdvanceTo(firstResult.Buffer.Start, firstResult.Buffer.End);

                    // Simulate disconnect
                    cts.Cancel();
                    await pipe.Writer.CompleteAsync(
                        new System.IO.IOException("Connection reset by peer")
                    );
                    var fallbackResult = await guardedReader.ReadAsync(cts.Token);

                    return (expected: expectedData, actual: fallbackResult.Buffer.ToArray());
                });

            var results = await Task.WhenAll(tasks);

            foreach (var (expected, actual) in results)
            {
                actual.ShouldBe(expected);
            }
        }

        [Fact]
        public async Task TryRead_Returns_Unconsumed_Buffer_On_Disconnect()
        {
            var pipe = new Pipe();
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var originalData = Encoding.UTF8.GetBytes("ABCDEF");
            await pipe.Writer.WriteAsync(originalData);

            guardedReader.TryRead(out var firstResult).ShouldBeTrue();
            firstResult.Buffer.Length.ShouldBe(originalData.Length);
            guardedReader.AdvanceTo(firstResult.Buffer.Start, firstResult.Buffer.End);

            cts.Cancel();
            await pipe.Writer.CompleteAsync(new System.IO.IOException("Connection reset by peer"));

            guardedReader.TryRead(out var fallbackResult).ShouldBeTrue();

            fallbackResult.IsCanceled.ShouldBeTrue();
            fallbackResult.Buffer.ToArray().ShouldBe(originalData);
        }

        [Fact]
        public async Task TryRead_Returns_False_When_No_Data_And_Not_Disconnected()
        {
            var pipe = new Pipe();
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var hasData = guardedReader.TryRead(out var result);

            hasData.ShouldBeFalse();
            result.ShouldBe(default(ReadResult));

            await pipe.Writer.CompleteAsync();
            await guardedReader.CompleteAsync();
        }

        [Fact]
        public async Task ReadAsync_Fallback_Buffer_Tracks_Multiple_Partial_Consumes_Before_Disconnect()
        {
            var pipe = new Pipe();
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var originalData = Encoding.UTF8.GetBytes("ABCDEFGHIJKL");
            await pipe.Writer.WriteAsync(originalData);

            // First read: all 12 bytes arrive, consume 4 (ABCD), examine all
            var r1 = await guardedReader.ReadAsync(cts.Token);
            r1.Buffer.Length.ShouldBe(12);
            guardedReader.AdvanceTo(r1.Buffer.GetPosition(4), r1.Buffer.End);

            // Write more data to unblock the second ReadAsync
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("MNOP"));

            // Second read: 8 unconsumed + 4 new = 12 bytes, consume 4 (EFGH), examine all
            var r2 = await guardedReader.ReadAsync(cts.Token);
            r2.Buffer.Length.ShouldBe(12);
            guardedReader.AdvanceTo(r2.Buffer.GetPosition(4), r2.Buffer.End);

            // Disconnect with 8 bytes unconsumed (IJKLMNOP)
            cts.Cancel();
            await pipe.Writer.CompleteAsync(new System.IO.IOException("Connection reset by peer"));

            var fallbackResult = await guardedReader.ReadAsync(cts.Token);

            fallbackResult.IsCanceled.ShouldBeTrue();
            fallbackResult.Buffer.ToArray().ShouldBe(Encoding.UTF8.GetBytes("IJKLMNOP"));
        }

        [Fact]
        public async Task ReadAsync_Passes_Through_IsCompleted_On_Normal_EndOfStream()
        {
            var pipe = new Pipe();
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var originalData = Encoding.UTF8.GetBytes("HELLO");
            await pipe.Writer.WriteAsync(originalData);
            await pipe.Writer.CompleteAsync();

            var r1 = await guardedReader.ReadAsync(cts.Token);
            r1.IsCanceled.ShouldBeFalse();
            r1.Buffer.ToArray().ShouldBe(originalData);
            guardedReader.AdvanceTo(r1.Buffer.End);

            if (!r1.IsCompleted)
            {
                // Some runtimes defer IsCompleted to the next read
                var r2 = await guardedReader.ReadAsync(cts.Token);
                r2.IsCompleted.ShouldBeTrue();
                r2.IsCanceled.ShouldBeFalse();
                r2.Buffer.IsEmpty.ShouldBeTrue();
            }

            await guardedReader.CompleteAsync();
        }

        [Fact]
        public async Task Complete_Clears_UnconsumedBuffer_And_Does_Not_Throw_After_Disconnect()
        {
            var pipe = new Pipe();
            var (guardedReader, cts) = CreateGuardedReader(pipe.Reader);

            var originalData = Encoding.UTF8.GetBytes("ABCDEF");
            await pipe.Writer.WriteAsync(originalData);

            var firstResult = await guardedReader.ReadAsync(cts.Token);
            guardedReader.AdvanceTo(firstResult.Buffer.Start, firstResult.Buffer.End);

            cts.Cancel();
            await pipe.Writer.CompleteAsync(new System.IO.IOException("Connection reset by peer"));

            var fallbackResult = await guardedReader.ReadAsync(cts.Token);
            fallbackResult.IsCanceled.ShouldBeTrue();
            fallbackResult.Buffer.IsEmpty.ShouldBeFalse();

            var exception = await Record.ExceptionAsync(async () =>
                await guardedReader.CompleteAsync()
            );
            exception.ShouldBeNull();
        }

        /// <summary>
        /// A MemoryPool wrapper that fills returned memory with 0xDE before handing it back
        /// to the inner pool. If the Pipe incorrectly returns memory to the pool while our
        /// ReadOnlySequence still holds a reference to it, any subsequent read from the
        /// sequence will reveal the poison bytes.
        /// </summary>
        private sealed class PoisoningMemoryPool : MemoryPool<byte>
        {
            private readonly MemoryPool<byte> _inner = MemoryPool<byte>.Shared;

            public override int MaxBufferSize => _inner.MaxBufferSize;

            public override IMemoryOwner<byte> Rent(int minBufferSize = -1) =>
                new PoisoningMemoryOwner(_inner.Rent(minBufferSize));

            protected override void Dispose(bool disposing)
            {
                // Intentionally empty; _inner is MemoryPool<byte>.Shared (a singleton that must not be disposed).
            }

            private sealed class PoisoningMemoryOwner : IMemoryOwner<byte>
            {
                private readonly IMemoryOwner<byte> _inner;

                public PoisoningMemoryOwner(IMemoryOwner<byte> inner) => _inner = inner;

                public Memory<byte> Memory => _inner.Memory;

                public void Dispose()
                {
                    // Poison the memory before returning it to the pool.
                    // If anyone still holds a reference to this memory, they will see 0xDE bytes.
                    _inner.Memory.Span.Fill(0xDE);
                    _inner.Dispose();
                }
            }
        }
    }
}

#endif
