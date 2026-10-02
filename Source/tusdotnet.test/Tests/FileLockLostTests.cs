using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using tusdotnet.Extensions.Store;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Extensions;
using tusdotnet.test.Helpers;
using Xunit;
#if pipelines
using System.IO.Pipelines;
#endif
#if netfull
using Microsoft.Owin.Testing;
#else
using Microsoft.AspNetCore.TestHost;
#endif

namespace tusdotnet.test.Tests
{
    public class FileLockLostTests
    {
        [Fact]
        public async Task GetFileLockLostToken_Returns_None_When_The_Lock_Cannot_Be_Lost()
        {
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile", 10, 5);
            CancellationToken? observedToken = null;
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    observedToken = call.Arg<Stream>().GetFileLockLostToken();
                    return 3;
                });

            using var server = TestServerFactory.Create(store);

            var response = await SendPatch(server);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            observedToken.ShouldBe(CancellationToken.None);
        }

        [Fact]
        public async Task GetFileLockLostToken_Returns_The_Token_Of_A_Leased_Lock()
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile", 10, 5);
            var observedToken = CancellationToken.None;
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    observedToken = call.Arg<Stream>().GetFileLockLostToken();
                    return 3;
                });

            using var server = CreateServer(store, lockProvider);

            var response = await SendPatch(server);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            observedToken.CanBeCanceled.ShouldBeTrue();
            lockProvider.LoseLock();
            observedToken.IsCancellationRequested.ShouldBeTrue();
        }

        [Fact]
        public async Task Returns_409_If_The_Store_Throws_TusUploadConflictException()
        {
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile", 10, 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(
                    new TusUploadConflictException("Upload was modified by another request")
                );

            using var server = TestServerFactory.Create(store);

            var response = await SendPatch(server);

            await response.ShouldBeErrorResponse(
                HttpStatusCode.Conflict,
                "Upload was modified by another request"
            );
            response.ShouldContainTusResumableHeader();
        }

        [Fact]
        public async Task Returns_409_If_The_Store_Is_Cancelled_Because_The_Lock_Was_Lost()
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile", 10, 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns<Task<long>>(call =>
                {
                    var lockLostToken = call.Arg<Stream>().GetFileLockLostToken();
                    lockProvider.LoseLock();
                    lockLostToken.ThrowIfCancellationRequested();
                    return Task.FromResult(3L);
                });

            using var server = CreateServer(store, lockProvider);

            var response = await SendPatch(server);

            await response.ShouldBeErrorResponse(
                HttpStatusCode.Conflict,
                "The file lock was lost while the file was being updated. Please try again"
            );
        }

        [Fact]
        public async Task Returns_409_If_A_Read_Is_Cancelled_Because_The_Lock_Was_Lost()
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile", 10, 5);
            var persisted = false;
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns<Task<long>>(async call =>
                {
                    var stream = call.Arg<Stream>();
                    lockProvider.LoseLock();
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                        call.Arg<CancellationToken>(),
                        stream.GetFileLockLostToken()
                    );

                    var buffer = new byte[10];
                    long totalRead = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                    {
                        totalRead += read;
                    }

                    persisted = true;
                    return totalRead;
                });

            using var server = CreateServer(store, lockProvider);

            var response = await SendPatch(server);

            await response.ShouldBeErrorResponse(
                HttpStatusCode.Conflict,
                "The file lock was lost while the file was being updated. Please try again"
            );
            persisted.ShouldBeFalse();
        }

        [Fact]
        public async Task Does_Not_Return_409_For_Cancellations_Unrelated_To_The_Lock()
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile", 10, 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            using var server = CreateServer(store, lockProvider);

            // The lock is still held, so the exception is not a lost lock and propagates as before.
            await Should.ThrowAsync<OperationCanceledException>(() => SendPatch(server));
        }

        [Fact]
        public async Task Returns_409_If_The_Store_Throws_TusUploadConflictException_During_Termination()
        {
            var store = (ITusTerminationStore)
                Substitute
                    .For<ITusStore, ITusTerminationStore>()
                    .WithExistingFile("testfile", 10, 5);
            store
                .DeleteFileAsync("testfile", Arg.Any<CancellationToken>())
                .ThrowsAsync(
                    new TusUploadConflictException("Upload was modified by another request")
                );

            using var server = TestServerFactory.Create((ITusStore)store);

            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .SendAsync("DELETE");

            await response.ShouldBeErrorResponse(
                HttpStatusCode.Conflict,
                "Upload was modified by another request"
            );
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Returns_201_With_The_Committed_Offset_If_The_Lock_Is_Lost_During_Creation_With_Upload(
            bool throwConflictException
        )
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = MockStoreHelper.CreateWithExtensions<ITusCreationStore>();
            ((ITusCreationStore)store)
                .CreateFileAsync(100, null, CancellationToken.None)
                .ReturnsForAnyArgs("testfile");
            store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(100);
            store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(0);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns<Task<long>>(call =>
                {
                    var lockLostToken = call.Arg<Stream>().GetFileLockLostToken();
                    lockProvider.LoseLock();

                    if (throwConflictException)
                        throw new TusUploadConflictException(
                            "Upload was modified by another request"
                        );

                    lockLostToken.ThrowIfCancellationRequested();
                    return Task.FromResult(3L);
                });

            using var server = CreateServer(store, lockProvider);

            var response = await server
                .CreateTusResumableRequest("/files")
                .AddHeader("Upload-Length", "100")
                .AddBody()
                .PostAsync();

            // The file was created, so the client gets its location and resumes from the committed offset.
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainHeader("Upload-Offset", "0");
            response.Headers.Location.ShouldNotBeNull();
        }

#if pipelines

        [Fact]
        public async Task GetFileLockLostToken_Returns_None_When_The_Lock_Cannot_Be_Lost_For_Pipelines()
        {
            var store = (ITusPipelineStore)
                Substitute.For<ITusPipelineStore>().WithExistingFile("testfile", 10, 5);
            CancellationToken? observedToken = null;
            store
                .AppendDataAsync("testfile", Arg.Any<PipeReader>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    observedToken = call.Arg<PipeReader>().GetFileLockLostToken();
                    return 3;
                });

            using var server = TestServerFactory.Create(store, usePipelinesIfAvailable: true);

            var response = await SendPatch(server);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            observedToken.ShouldBe(CancellationToken.None);
        }

        [Fact]
        public async Task GetFileLockLostToken_Returns_The_Token_Of_A_Leased_Lock_For_Pipelines()
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = (ITusPipelineStore)
                Substitute.For<ITusPipelineStore>().WithExistingFile("testfile", 10, 5);
            var observedToken = CancellationToken.None;
            store
                .AppendDataAsync("testfile", Arg.Any<PipeReader>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    observedToken = call.Arg<PipeReader>().GetFileLockLostToken();
                    return 3;
                });

            using var server = CreateServer(store, lockProvider, usePipelines: true);

            var response = await SendPatch(server);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            observedToken.CanBeCanceled.ShouldBeTrue();
            lockProvider.LoseLock();
            observedToken.IsCancellationRequested.ShouldBeTrue();
        }

        [Fact]
        public async Task Returns_409_If_The_Store_Is_Cancelled_Because_The_Lock_Was_Lost_For_Pipelines()
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = (ITusPipelineStore)
                Substitute.For<ITusPipelineStore>().WithExistingFile("testfile", 10, 5);
            store
                .AppendDataAsync("testfile", Arg.Any<PipeReader>(), Arg.Any<CancellationToken>())
                .Returns<Task<long>>(call =>
                {
                    var lockLostToken = call.Arg<PipeReader>().GetFileLockLostToken();
                    lockProvider.LoseLock();
                    lockLostToken.ThrowIfCancellationRequested();
                    return Task.FromResult(3L);
                });

            using var server = CreateServer(store, lockProvider, usePipelines: true);

            var response = await SendPatch(server);

            await response.ShouldBeErrorResponse(
                HttpStatusCode.Conflict,
                "The file lock was lost while the file was being updated. Please try again"
            );
        }

        [Fact]
        public async Task Returns_409_If_A_Read_Is_Cancelled_Because_The_Lock_Was_Lost_For_Pipelines()
        {
            var lockProvider = new LeasedFileLockProvider();
            var store = (ITusPipelineStore)
                Substitute.For<ITusPipelineStore>().WithExistingFile("testfile", 10, 5);
            var persisted = false;
            store
                .AppendDataAsync("testfile", Arg.Any<PipeReader>(), Arg.Any<CancellationToken>())
                .Returns<Task<long>>(async call =>
                {
                    var reader = call.Arg<PipeReader>();
                    lockProvider.LoseLock();
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                        call.Arg<CancellationToken>(),
                        reader.GetFileLockLostToken()
                    );

                    long totalRead = 0;
                    while (true)
                    {
                        var result = await reader.ReadAsync(cts.Token);
                        totalRead += result.Buffer.Length;
                        reader.AdvanceTo(result.Buffer.End);

                        if (result.IsCanceled || result.IsCompleted)
                        {
                            break;
                        }
                    }

                    persisted = true;
                    return totalRead;
                });

            using var server = CreateServer(store, lockProvider, usePipelines: true);

            var response = await SendPatch(server);

            await response.ShouldBeErrorResponse(
                HttpStatusCode.Conflict,
                "The file lock was lost while the file was being updated. Please try again"
            );
            persisted.ShouldBeFalse();
        }
#endif

        private static TestServer CreateServer(
            ITusStore store,
            ITusFileLockProvider lockProvider,
            bool usePipelines = false
        )
        {
            return TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    FileLockProvider = lockProvider,
#if pipelines
                    UsePipelinesIfAvailable = usePipelines,
#endif
                }
            );
        }

        private static Task<HttpResponseMessage> SendPatch(TestServer server)
        {
            return server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");
        }

        private sealed class LeasedFileLockProvider : ITusFileLockProvider
        {
            private readonly CancellationTokenSource _lockLost = new();

            public void LoseLock() => _lockLost.Cancel();

            public Task<ITusFileLock> AquireLock(string fileId)
            {
                return Task.FromResult<ITusFileLock>(new LeasedFileLock(_lockLost.Token));
            }

            private sealed class LeasedFileLock : ITusLeasedFileLock
            {
                private bool _hasLock;

                public LeasedFileLock(CancellationToken lockLostToken)
                {
                    LockLostToken = lockLostToken;
                }

                public CancellationToken LockLostToken { get; }

                public Task<bool> Lock()
                {
                    _hasLock = true;
                    return Task.FromResult(true);
                }

                public Task ReleaseIfHeld()
                {
                    _hasLock = false;
                    return Task.FromResult(_hasLock);
                }
            }
        }
    }
}
