using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using tusdotnet.Extensions.Store;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Streams;
using Xunit;
#if pipelines
using System.IO.Pipelines;
using System.Text;
using tusdotnet.Models.PipeReaders;
#endif

namespace tusdotnet.test.Tests.ModelTests
{
    // The guards are stacked on a real ClientDisconnectGuard in the same order as the factories, because the
    // disconnect guard swallows cancelled reads and must let a read cancelled by the lost lock through.
    public class FileLockLostGuardTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Stream_ReadAsync_Throws_When_The_Lock_Is_Lost_While_Waiting_For_Data()
        {
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var stream = new FileLockLostGuardedReadOnlyStream(
                new ClientDisconnectGuardedReadOnlyStream(new NeverEndingReadStream(), guard),
                lockLostCts.Token
            );

            var read = stream.ReadAsync(new byte[10], 0, 10, requestCts.Token);
            lockLostCts.Cancel();

            await ShouldThrowWithinTimeout(read);
        }

        [Fact]
        public async Task Stream_ReadAsync_Throws_When_The_Lock_Was_Lost_Before_The_Read()
        {
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var stream = new FileLockLostGuardedReadOnlyStream(
                new ClientDisconnectGuardedReadOnlyStream(new MemoryStream(new byte[10]), guard),
                lockLostCts.Token
            );
            lockLostCts.Cancel();

            await Should.ThrowAsync<FileLockLostException>(() =>
                stream.ReadAsync(new byte[10], 0, 10, requestCts.Token)
            );
        }

        [Fact]
        public async Task Stream_ReadAsync_Returns_Data_While_The_Lock_Is_Held()
        {
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var stream = new FileLockLostGuardedReadOnlyStream(
                new ClientDisconnectGuardedReadOnlyStream(new MemoryStream(new byte[10]), guard),
                lockLostCts.Token
            );

            var read = await stream.ReadAsync(new byte[20], 0, 20, requestCts.Token);

            read.ShouldBe(10);
        }

        [Fact]
        public async Task Stream_ReadAsync_Treats_A_Client_Disconnect_As_End_Of_Body_While_The_Lock_Is_Held()
        {
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var stream = new FileLockLostGuardedReadOnlyStream(
                new ClientDisconnectGuardedReadOnlyStream(new NeverEndingReadStream(), guard),
                lockLostCts.Token
            );

            var read = stream.ReadAsync(new byte[10], 0, 10, guard.GuardedToken);
            requestCts.Cancel();

            (await WithinTimeout(read)).ShouldBe(0);
        }

        [Fact]
        public void Stream_GetFileLockLostToken_Finds_The_Guard_Under_The_Checksum_Wrapper()
        {
            // Same order as GuardedStreamFactory.
            var (_, lockLostCts, guard) = CreateDisconnectGuard();
            Stream stream = new MaxReadSizeGuardedReadOnlyStream(
                new ClientDisconnectGuardedReadOnlyStream(new MemoryStream(), guard),
                0,
                10,
                MaxReadSizeExceededException.SizeSourceType.UploadLength
            );
            stream = new FileLockLostGuardedReadOnlyStream(stream, lockLostCts.Token);
            stream = new ChecksumAwareStream(stream, new Checksum("sha1", new byte[20]));

            stream.GetFileLockLostToken().ShouldBe(lockLostCts.Token);
            stream.GetUploadChecksumInfo().ShouldNotBeNull();
        }

#if pipelines

        [Fact]
        public async Task PipeReader_ReadAsync_Throws_When_The_Lock_Is_Lost_While_Waiting_For_Data()
        {
            var pipe = new Pipe();
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var reader = new FileLockLostGuardedPipeReader(
                new ClientDisconnectGuardedPipeReader(pipe.Reader, guard),
                lockLostCts.Token
            );

            var read = reader.ReadAsync(requestCts.Token).AsTask();
            lockLostCts.Cancel();

            await ShouldThrowWithinTimeout(read);
        }

        [Fact]
        public async Task PipeReader_ReadAsync_Throws_When_The_Lock_Was_Lost_Before_The_Read()
        {
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("ABCDEF"));
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var reader = new FileLockLostGuardedPipeReader(
                new ClientDisconnectGuardedPipeReader(pipe.Reader, guard),
                lockLostCts.Token
            );
            lockLostCts.Cancel();

            await Should.ThrowAsync<FileLockLostException>(() =>
                reader.ReadAsync(requestCts.Token).AsTask()
            );
        }

        [Fact]
        public async Task PipeReader_TryRead_Throws_When_The_Lock_Is_Lost()
        {
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("ABCDEF"));
            var (_, lockLostCts, guard) = CreateDisconnectGuard();
            var reader = new FileLockLostGuardedPipeReader(
                new ClientDisconnectGuardedPipeReader(pipe.Reader, guard),
                lockLostCts.Token
            );
            lockLostCts.Cancel();

            Should.Throw<FileLockLostException>(() => reader.TryRead(out _));
        }

        [Fact]
        public async Task PipeReader_AdvanceTo_Throws_When_The_Lock_Is_Lost_After_The_Read()
        {
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("ABCDEF"));
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var reader = new FileLockLostGuardedPipeReader(
                new ClientDisconnectGuardedPipeReader(pipe.Reader, guard),
                lockLostCts.Token
            );

            var result = await reader.ReadAsync(requestCts.Token);
            lockLostCts.Cancel();

            Should.Throw<FileLockLostException>(() => reader.AdvanceTo(result.Buffer.End));
            Should.Throw<FileLockLostException>(() =>
                reader.AdvanceTo(result.Buffer.Start, result.Buffer.End)
            );
        }

        [Fact]
        public async Task PipeReader_Returns_Data_While_The_Lock_Is_Held()
        {
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("ABCDEF"));
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var reader = new FileLockLostGuardedPipeReader(
                new ClientDisconnectGuardedPipeReader(pipe.Reader, guard),
                lockLostCts.Token
            );

            reader.TryRead(out var tryReadResult).ShouldBeTrue();
            tryReadResult.Buffer.Length.ShouldBe(6);
            reader.AdvanceTo(tryReadResult.Buffer.Start, tryReadResult.Buffer.End);

            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("GH"));
            var readResult = await reader.ReadAsync(requestCts.Token);
            readResult.Buffer.Length.ShouldBe(8);
        }

        [Fact]
        public async Task PipeReader_Treats_A_Client_Disconnect_As_A_Cancelled_Read_While_The_Lock_Is_Held()
        {
            var pipe = new Pipe();
            var (requestCts, lockLostCts, guard) = CreateDisconnectGuard();
            var reader = new FileLockLostGuardedPipeReader(
                new ClientDisconnectGuardedPipeReader(pipe.Reader, guard),
                lockLostCts.Token
            );

            var read = reader.ReadAsync(guard.GuardedToken).AsTask();
            requestCts.Cancel();

            (await WithinTimeout(read)).IsCanceled.ShouldBeTrue();
        }

        [Fact]
        public void PipeReader_GetFileLockLostToken_Finds_The_Guard_Under_The_Checksum_Wrapper()
        {
            // Same order as GuardedPipeReaderFactory.
            var (_, lockLostCts, guard) = CreateDisconnectGuard();
            PipeReader reader = new MaxReadSizeGuardedPipeReader(
                new ClientDisconnectGuardedPipeReader(new Pipe().Reader, guard),
                0,
                10,
                MaxReadSizeExceededException.SizeSourceType.UploadLength
            );
            reader = new FileLockLostGuardedPipeReader(reader, lockLostCts.Token);
            reader = new ChecksumAwarePipeReader(reader, new Checksum("sha1", new byte[20]));

            reader.GetFileLockLostToken().ShouldBe(lockLostCts.Token);
            reader.GetUploadChecksumInfo().ShouldNotBeNull();
        }
#endif

        // A class rather than a tuple because net452 has no System.ValueTuple.
        private sealed class DisconnectGuardSetup
        {
            private readonly CancellationTokenSource _requestCts;
            private readonly CancellationTokenSource _lockLostCts;
            private readonly ClientDisconnectGuardWithTimeout _guard;

            public DisconnectGuardSetup(
                CancellationTokenSource requestCts,
                CancellationTokenSource lockLostCts,
                ClientDisconnectGuardWithTimeout guard
            )
            {
                _requestCts = requestCts;
                _lockLostCts = lockLostCts;
                _guard = guard;
            }

            public void Deconstruct(
                out CancellationTokenSource requestCts,
                out CancellationTokenSource lockLostCts,
                out ClientDisconnectGuardWithTimeout guard
            )
            {
                requestCts = _requestCts;
                lockLostCts = _lockLostCts;
                guard = _guard;
            }
        }

        private static DisconnectGuardSetup CreateDisconnectGuard()
        {
            var requestCts = new CancellationTokenSource();
            var lockLostCts = new CancellationTokenSource();
            var guard = new ClientDisconnectGuardWithTimeout(
                TimeSpan.FromSeconds(60),
                requestCts.Token
            )
            {
                // ContextAdapter sets this when the runner acquires a leased lock.
                FileLockLostToken = lockLostCts.Token,
            };

            return new DisconnectGuardSetup(requestCts, lockLostCts, guard);
        }

        private static async Task<T> WithinTimeout<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TestTimeout));
            completed.ShouldBe(task, "The read did not complete");
            return await task;
        }

        private static async Task ShouldThrowWithinTimeout<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TestTimeout));
            completed.ShouldBe(task, "The read was not stopped by the lost lock");
            await Should.ThrowAsync<FileLockLostException>(() => task);
        }

        // A request body whose client has stopped sending: a read completes only when it is cancelled.
        private sealed class NeverEndingReadStream : ReadOnlyStream
        {
            public NeverEndingReadStream()
                : base(Null) { }

            public override async Task<int> ReadAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken
            )
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }
        }
    }
}
