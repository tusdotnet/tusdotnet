using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.OngoingUploadManagers;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class InvertedLockTests
    {
        private const HttpStatusCode HttpStatusCodeLocked = (HttpStatusCode)423;

        [Fact]
        public async Task Head_Preempts_Ongoing_Patch_When_Using_Inverted_Locks()
        {
            var fileId = Guid.NewGuid().ToString("N");
            var patchStarted = new TaskCompletionSource<bool>();
            var patchCancelled = false;

            var store = Substitute.For<ITusStore>();
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
            store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async info =>
                {
                    var ct = info.Arg<CancellationToken>();
                    patchStarted.TrySetResult(true);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(20), ct);
                        return 10L;
                    }
                    catch (OperationCanceledException)
                    {
                        patchCancelled = true;
                        throw;
                    }
                });

            using var server = TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    OngoingUploadManager = new OngoingUploadManagerInMemory(),
                }
            );

            var patchTask = server
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await patchStarted.Task;

            var headResponse = await server
                .CreateTusResumableRequest($"/files/{fileId}")
                .SendAsync("HEAD");

            await Should.ThrowAsync<Exception>(patchTask);

            headResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            patchCancelled.ShouldBeTrue();
        }

        [Fact]
        public async Task Patch_Preempts_Previous_Patch_When_Using_Inverted_Locks()
        {
            var fileId = Guid.NewGuid().ToString("N");
            var firstPatchStarted = new TaskCompletionSource<bool>();
            var firstPatchCancelled = false;
            var appendInvocation = 0;

            var store = Substitute.For<ITusStore>();
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
            store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async info =>
                {
                    var invocation = Interlocked.Increment(ref appendInvocation);
                    var ct = info.Arg<CancellationToken>();

                    if (invocation == 1)
                    {
                        firstPatchStarted.TrySetResult(true);

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10), ct);
                            return 10L;
                        }
                        catch (OperationCanceledException)
                        {
                            firstPatchCancelled = true;
                            throw;
                        }
                    }

                    return 10L;
                });

            var sharedManager = new OngoingUploadManagerInMemory();

            using var server1 = TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    OngoingUploadManager = sharedManager,
                }
            );
            using var server2 = TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    OngoingUploadManager = sharedManager,
                }
            );

            var patch1 = server1
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await firstPatchStarted.Task;

            var patch2 = server2
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            var patch2Response = await patch2;
            await Should.ThrowAsync<Exception>(patch1);

            firstPatchCancelled.ShouldBeTrue();
            patch2Response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Returns_423_To_New_Request_If_Previous_Patch_Does_Not_Stop_Within_Timeout()
        {
            var fileId = Guid.NewGuid().ToString("N");
            var firstPatchStarted = new TaskCompletionSource<bool>();
            var appendInvocation = 0;

            var store = Substitute.For<ITusStore>();
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
            store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async info =>
                {
                    var invocation = Interlocked.Increment(ref appendInvocation);
                    var ct = info.Arg<CancellationToken>();
                    if (invocation == 1)
                    {
                        firstPatchStarted.TrySetResult(true);

                        try
                        {
                            await Task.Delay(1000, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            // Intentionally ignored to emulate a request that keeps running
                            // and does not observe cancellation in a timely manner.
                        }

                        await Task.Delay(1000);

                        return 10L;
                    }

                    return 10L;
                });

            using var server = TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    OngoingUploadManager = new OngoingUploadManagerInMemory(
                        TimeSpan.FromMilliseconds(100)
                    ),
                }
            );

            var patch1 = server
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await firstPatchStarted.Task;

            var patch2 = await server
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await Should.ThrowAsync<Exception>(patch1);

            patch2.StatusCode.ShouldBe(HttpStatusCodeLocked);
        }

        [Fact]
        public async Task Delete_Preempts_Ongoing_Patch_When_Using_Inverted_Locks()
        {
            var fileId = Guid.NewGuid().ToString("N");
            var patchStarted = new TaskCompletionSource<bool>();
            var patchCancelled = false;

            var store = Substitute.For<ITusStore, ITusTerminationStore>();
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
            store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async info =>
                {
                    var ct = info.Arg<CancellationToken>();
                    patchStarted.TrySetResult(true);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(20), ct);
                        return 10L;
                    }
                    catch (OperationCanceledException)
                    {
                        patchCancelled = true;
                        throw;
                    }
                });

            ((ITusTerminationStore)store)
                .DeleteFileAsync(fileId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(0));

            using var server = TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    OngoingUploadManager = new OngoingUploadManagerInMemory(),
                }
            );

            var patchTask = server
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await patchStarted.Task;

            var deleteResponse = await server
                .CreateTusResumableRequest($"/files/{fileId}")
                .SendAsync("DELETE");

            await Should.ThrowAsync<Exception>(patchTask);

            deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            patchCancelled.ShouldBeTrue();
        }

        [Fact]
        public async Task Head_Preempts_Patch_Across_Two_Servers_Using_Disk_Manager()
        {
            var fileId = Guid.NewGuid().ToString("N");
            var patchStarted = new TaskCompletionSource<bool>();
            var patchCancelled = false;
            var sharedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(sharedPath);

            try
            {
                var store = Substitute.For<ITusStore>();
                store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
                store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
                store
                    .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                    .Returns(async info =>
                    {
                        var ct = info.Arg<CancellationToken>();
                        patchStarted.TrySetResult(true);

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10), ct);
                            return 10L;
                        }
                        catch (OperationCanceledException)
                        {
                            patchCancelled = true;
                            throw;
                        }
                    });

                using var server1 = TestServerFactory.Create(
                    new DefaultTusConfiguration
                    {
                        UrlPath = "/files",
                        Store = store,
                        OngoingUploadManager = new OngoingUploadManagerDiskBased(sharedPath),
                    }
                );
                using var server2 = TestServerFactory.Create(
                    new DefaultTusConfiguration
                    {
                        UrlPath = "/files",
                        Store = store,
                        OngoingUploadManager = new OngoingUploadManagerDiskBased(sharedPath),
                    }
                );

                var patchTask = server1
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .AddHeader("Upload-Offset", "0")
                    .AddBody()
                    .SendAsync("PATCH");

                await patchStarted.Task;

                var headResponse = await server2
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .SendAsync("HEAD");

                await Should.ThrowAsync<Exception>(patchTask);

                headResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
                patchCancelled.ShouldBeTrue();
            }
            finally
            {
                if (Directory.Exists(sharedPath))
                {
                    Directory.Delete(sharedPath, recursive: true);
                }
            }
        }

        [Fact]
        public async Task Delete_Preempts_Patch_Across_Two_Servers_Using_Disk_Manager()
        {
            var fileId = Guid.NewGuid().ToString("N");
            var patchStarted = new TaskCompletionSource<bool>();
            var patchCancelled = false;
            var sharedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(sharedPath);

            try
            {
                var store = Substitute.For<ITusStore, ITusTerminationStore>();
                store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
                store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
                store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async info =>
                {
                    var ct = info.Arg<CancellationToken>();
                        patchStarted.TrySetResult(true);

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10), ct);
                            return 10L;
                        }
                        catch (OperationCanceledException)
                        {
                            patchCancelled = true;
                            throw;
                        }
                    });

                ((ITusTerminationStore)store)
                    .DeleteFileAsync(fileId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(0));

                using var server1 = TestServerFactory.Create(
                    new DefaultTusConfiguration
                    {
                        UrlPath = "/files",
                        Store = store,
                        OngoingUploadManager = new OngoingUploadManagerDiskBased(sharedPath),
                    }
                );
                using var server2 = TestServerFactory.Create(
                    new DefaultTusConfiguration
                    {
                        UrlPath = "/files",
                        Store = store,
                        OngoingUploadManager = new OngoingUploadManagerDiskBased(sharedPath),
                    }
                );

                var patchTask = server1
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .AddHeader("Upload-Offset", "0")
                    .AddBody()
                    .SendAsync("PATCH");

                await patchStarted.Task;

                var deleteResponse = await server2
                    .CreateTusResumableRequest($"/files/{fileId}")
                    .SendAsync("DELETE");

                await Should.ThrowAsync<Exception>(patchTask);

                deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                patchCancelled.ShouldBeTrue();
            }
            finally
            {
                if (Directory.Exists(sharedPath))
                {
                    Directory.Delete(sharedPath, recursive: true);
                }
            }
        }

        [Fact]
        public async Task Third_Request_Is_Blocked_If_Previous_Patch_Did_Not_Stop_Within_Timeout()
        {
            // A is running, B preempts but times out (A ignores cancellation).
            // C then arrives — C must be blocked/rejected, not allowed to run concurrently with A.
            var fileId = Guid.NewGuid().ToString("N");
            var patchAStarted = new TaskCompletionSource<bool>();
            var patchARelease = new TaskCompletionSource<bool>();
            var appendInvocation = 0;

            var store = Substitute.For<ITusStore>();
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
            store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async info =>
                {
                    var invocation = Interlocked.Increment(ref appendInvocation);
                    if (invocation == 1)
                    {
                        patchAStarted.TrySetResult(true);
                        // Wait until the test releases A, ignoring cancellation.
                        await patchARelease.Task;
                    }
                    return 10L;
                });

            var sharedManager = new OngoingUploadManagerInMemory(TimeSpan.FromMilliseconds(100));

            using var serverA = TestServerFactory.Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                OngoingUploadManager = sharedManager,
            });
            using var serverB = TestServerFactory.Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                OngoingUploadManager = sharedManager,
            });
            using var serverC = TestServerFactory.Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                OngoingUploadManager = sharedManager,
            });

            // Start A
            var patchA = serverA
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await patchAStarted.Task;

            // B tries to preempt A but times out — A is still running
            var patchB = await serverB
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            patchB.StatusCode.ShouldBe(HttpStatusCodeLocked);

            // C arrives while A is still running — must also be blocked
            var patchC = await serverC
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            patchC.StatusCode.ShouldBe(HttpStatusCodeLocked);

            // Now release A
            patchARelease.TrySetResult(true);
            await Should.ThrowAsync<Exception>(patchA);
        }

        [Fact]
        public async Task After_Timeout_And_Original_Release_Next_Request_Can_Acquire()
        {
            // A is running, B preempts but times out. A then finishes normally.
            // D (the next request) should be able to acquire and run successfully.
            var fileId = Guid.NewGuid().ToString("N");
            var patchAStarted = new TaskCompletionSource<bool>();
            var patchARelease = new TaskCompletionSource<bool>();
            var appendInvocation = 0;

            var store = Substitute.For<ITusStore>();
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(100L);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(0L);
            store
                .AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async info =>
                {
                    var invocation = Interlocked.Increment(ref appendInvocation);
                    if (invocation == 1)
                    {
                        patchAStarted.TrySetResult(true);
                        await patchARelease.Task;
                    }
                    return 10L;
                });

            var sharedManager = new OngoingUploadManagerInMemory(TimeSpan.FromMilliseconds(100));

            using var serverA = TestServerFactory.Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                OngoingUploadManager = sharedManager,
            });
            using var serverB = TestServerFactory.Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                OngoingUploadManager = sharedManager,
            });
            using var serverD = TestServerFactory.Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                OngoingUploadManager = sharedManager,
            });

            var patchA = serverA
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await patchAStarted.Task;

            // B times out
            var patchB = await serverB
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            patchB.StatusCode.ShouldBe(HttpStatusCodeLocked);

            // Release A normally
            patchARelease.TrySetResult(true);
            await Should.ThrowAsync<Exception>(patchA);

            // D should now be able to acquire
            var patchD = await serverD
                .CreateTusResumableRequest($"/files/{fileId}")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            patchD.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

    }
}
