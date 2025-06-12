using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using tusdotnet.test.Helpers;
using Xunit;
#if netfull
using Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.Builder;
#endif

namespace tusdotnet.test.Tests
{
    public class WriteFileStreamsTests
    {
        // HttpStatusCode.Locked is not available on netstandard nor net452.
        private const HttpStatusCode HttpStatusCodeLocked = (HttpStatusCode)423;

        private bool _callForwarded;
        private bool _onAuthorizeWasCalled;
        private IntentType? _onAuthorizeWasCalledWithIntent;

        [Fact]
        public async Task Ignores_Request_If_Url_Does_Not_Match()
        {
            using var server = TestServerFactory.CreateWithForwarding(
                Substitute.For<ITusStore>(),
                () => _onAuthorizeWasCalled = true,
                () => _callForwarded = true
            );

            await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("PATCH");
            AssertAndResetForwardCall(true);

            await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .SendAsync("PATCH");
            AssertAndResetForwardCall(false);

            await server
                .CreateRequest("/otherfiles/testfile")
                .AddTusResumableHeader()
                .SendAsync("PATCH");
            AssertAndResetForwardCall(true);
        }

        [Fact]
        public async Task Returns_404_Not_Found_If_File_Was_Not_Found()
        {
            using var server = TestServerFactory.Create(Substitute.For<ITusStore>());
            var response = await server
                .CreateRequest("/files/testfile")
                .And(m => m.AddBody())
                .AddHeader("Upload-Offset", "0")
                .AddTusResumableHeader()
                .SendAsync("PATCH");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.ShouldContainHeader("Cache-Control", "no-store");
            response.ShouldContainTusResumableHeader();
        }

        [Theory]
        [InlineData("text/plain")]
        [InlineData(null)]
        [InlineData("application/octet-stream")]
        [InlineData("application/json")]
        public async Task Returns_415_Unsupported_Media_Type_If_An_Incorrect_Content_Type_Is_Provided(
            string contentType
        )
        {
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile");
            using var server = TestServerFactory.Create(store);
            var requestBuilder = server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "0");

            if (contentType != null)
            {
                requestBuilder = requestBuilder.AddBody(contentType);
            }

            var response = await requestBuilder.SendAsync("PATCH");

            await response.ShouldBeErrorResponse(
                HttpStatusCode.UnsupportedMediaType,
                $"Content-Type {contentType} is invalid. Must be application/offset+octet-stream"
            );
            response.ShouldContainTusResumableHeader();
        }

        [Fact]
        public async Task Returns_400_Bad_Request_For_Missing_Upload_Offset_Header()
        {
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile");

            using var server = TestServerFactory.Create(store);
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddBody()
                .SendAsync("patch");

            await response.ShouldBeErrorResponse(
                HttpStatusCode.BadRequest,
                "Missing Upload-Offset header"
            );
        }

        [Theory]
        [InlineData("uploadoffset", "")]
        [InlineData("1.0.1", "")]
        [InlineData("0.2", "")]
        [InlineData("-100", "Header Upload-Offset must be a positive number")]
        public async Task Returns_400_Bad_Request_For_Invalid_Upload_Offset_Header(
            string uploadOffset,
            string expectedServerErrorMessage
        )
        {
            var store = Substitute.For<ITusStore>().WithExistingFile("testfile");

            using var server = TestServerFactory.Create(store);
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddBody()
                .AddHeader("Upload-Offset", uploadOffset)
                .SendAsync("patch");

            await response.ShouldBeErrorResponse(
                HttpStatusCode.BadRequest,
                string.IsNullOrEmpty(expectedServerErrorMessage)
                    ? "Could not parse Upload-Offset header"
                    : expectedServerErrorMessage
            );
        }

        [Theory]
        [InlineData(5)]
        [InlineData(100)]
        public async Task Returns_409_Conflict_If_Upload_Offset_Does_Not_Match_File_Offset(
            int offset
        )
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 15, uploadOffset: 10);

            using var server = TestServerFactory.Create(store);
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", offset.ToString())
                .AddBody()
                .SendAsync("PATCH");

            await response.ShouldBeErrorResponse(
                HttpStatusCode.Conflict,
                $"Offset does not match file. File offset: 10. Request offset: {offset}"
            );
        }

        [Fact]
        public async Task Returns_400_Bad_Request_If_Upload_Is_Already_Complete()
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 10);

            using var server = TestServerFactory.Create(store);
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "10")
                .AddBody()
                .SendAsync("PATCH");

            await response.ShouldBeErrorResponse(
                HttpStatusCode.BadRequest,
                "Upload is already complete."
            );
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_204_No_Content_On_Success(string methodToUse)
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(5);

            using var server = TestServerFactory.Create(store);
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Response_Contains_The_Correct_Headers_On_Success(string methodToUse)
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(5);

            using var server = TestServerFactory.Create(store);
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .SendAsync(methodToUse);

            response.ShouldContainTusResumableHeader();
            response.ShouldContainHeader("Upload-Offset", "10");
            response.ShouldNotContainHeaders("Upload-Expires");
        }

        [Fact]
        public async Task Returns_Store_Exceptions_As_400_Bad_Request()
        {
            // This test does not work properly using the OWIN TestServer.
            // It will always throw an exception instead of returning the proper error message to the client.

            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Throws(new TusStoreException("Test exception"));

            using var server = TestServerFactory.Create(store);
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.Content.ReadAsStringAsync().Result.ShouldBe("Test exception");
        }

        [Theory]
        [PipelineDisconnectEmulationData]
        public async Task Handles_Abrupt_Disconnects_Gracefully(string pipeline)
        {
            // This test differs in structure from the others as this test needs to test that tusdotnet handles pipeline specifics correctly.
            // We can only emulate the pipelines so this test might not be 100% accurate.
            // Also we must bypass the TestServer and go directly for the TusProtcolHandler as we would otherwise introduce yet another pipeline.

            var pipelineDetails = PipelineDisconnectEmulationDataAttribute.GetInfo(pipeline);

            var cts = new CancellationTokenSource();
            var httpContext = new DefaultHttpContext { RequestAborted = cts.Token };

            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);

            var requestStream = Substitute.For<Stream>();
            requestStream
                .ReadAsync(
                    Arg.Any<byte[]>(),
                    Arg.Any<int>(),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>()
                )
                .ThrowsForAnyArgs(_ =>
                {
                    if (pipelineDetails.FlagsCancellationTokenAsCancelled)
                    {
                        cts.Cancel();
                    }
                    throw pipelineDetails.ExceptionThatIsThrown;
                });

            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .ReturnsForAnyArgs<Task<long>>(async callInfo =>
                    await callInfo
                        .Arg<Stream>()
                        .ReadAsync(null, 0, 0, callInfo.Arg<CancellationToken>())
                );

            var request = new RequestAdapter()
            {
                Headers = RequestHeaders.FromDictionary(
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Content-Type", "application/offset+octet-stream" },
                        { "Tus-Resumable", "1.0.0" },
                        { "Upload-Offset", "5" },
                    }
                ),
                Method = "PATCH",
                Body = requestStream,
                RequestUri = new Uri("https://localhost:8080/files/testfile"),
            };

            var config = new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                FileLockProvider = new TestServerInMemoryFileLockProvider(),
            };

            var context = new ContextAdapter(
                "/files",
                requestPathBase: null,
                MiddlewareUrlHelper.Instance,
                request,
                config,
                httpContext
            );

            var handled = await TusV1EventRunner.Invoke(context);

            handled.ShouldBe(ResultType.StopExecution);
            context.Response.Status.ShouldBe((HttpStatusCode)0);
            context.Response.Headers.Count.ShouldBe(0);
            context.Response.Message.ShouldBeNull();
        }

#if netstandard

        [Fact]
        public async Task Handles_Read_Timeouts_Gracefully()
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);

            store
                .AppendDataAsync(default, default, default)
                .ReturnsForAnyArgs(async callInfo =>
                {
                    var reader = callInfo.Arg<Stream>();
                    var ct = callInfo.Arg<CancellationToken>();
                    var res = await reader.ReadAsync(new byte[100], 0, 5, ct);

                    return (long)res;
                });

            var config = new DefaultTusConfiguration
            {
                ClientReadTimeout = TimeSpan.FromMilliseconds(100),
                UrlPath = "/files",
                Store = store,

#if pipelines
                UsePipelinesIfAvailable = false
#endif
            };

            bool failedSuccessfully;

            try
            {
                using var server = TestServerFactory.Create(config);
                var response = await server
                    .CreateTusResumableRequest("/files/testfile")
                    .AddHeader("Upload-Offset", "5")
                    .And(m =>
                    {
                        m.Content = new StreamContent(
                            new SlowMemoryStream(
                                new byte[5],
                                delayPerReadInMs: 500,
                                allowCancellation: true
                            )
                        );
                        m.Content.Headers.Add("Content-Type", "application/offset+octet-stream");
                    })
                    .SendAsync("PATCH");

                // The test server in .NET Core < 3 does not properly disconnect when the server kills the connection.
                // Instead it returns a default object with status 200 OK.
                // Since tus does not allow this status in this case and tusdotnet does not set anything (since the connection was killed)
                // we can use this state to determine that the client was disconnected.
                failedSuccessfully =
                    response.StatusCode == HttpStatusCode.OK
                    && !response.Headers.TryGetValues("Tus-Resumable", out var _);
            }
            catch (Exception ex)
            {
                failedSuccessfully = ex.Message == "The application aborted the request.";
            }

            failedSuccessfully.ShouldBeTrue("Request did not fail successfully");
        }
#endif

        [Fact]
        public async Task Returns_423_Locked_If_Multiple_Requests_Try_To_Patch_The_Same_File()
        {
            var random = new Random();
            var offset = 5;
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: offset);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    // Emulate some latency in the request.
                    Thread.Sleep(random.Next(100, 301));
                    offset += 3;
                    return 3;
                });

            using var server = TestServerFactory.Create(store);
            // Duplicated code due to:
            // "System.InvalidOperationException: The request message was already sent. Cannot send the same request message multiple times."
            var task1 = server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");

            var task2 = server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");

            await Task.WhenAll(task1, task2);

            if (task1.Result.StatusCode == HttpStatusCode.NoContent)
            {
                task1.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                task2.Result.StatusCode.ShouldBe(HttpStatusCodeLocked);
            }
            else
            {
                task1.Result.StatusCode.ShouldBe(HttpStatusCodeLocked);
                task2.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }
        }

        [Fact]
        public async Task Runs_OnFileCompleteAsync_And_OnUploadCompleteAsync_When_Upload_Is_Complete()
        {
            // Old callback handler
            var onUploadCompleteCallCounts = new Dictionary<string, int>(2)
            {
                { "file1", 0 },
                { "file2", 0 },
            };

            // New event handler
            var onFileCompleteAsyncCallbackCounts = new Dictionary<string, int>(2)
            {
                { "file1", 0 },
                { "file2", 0 },
            };

            var firstOffset = 3;
            var secondOffset = 2;

            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("file1", _ => 6, _ => firstOffset);
            store
                .AppendDataAsync("file1", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(3)
                .AndDoes(_ => firstOffset += 3);

            store = store.WithExistingFile("file2", _ => 6, _ => secondOffset);
            store
                .AppendDataAsync("file2", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(3)
                .AndDoes(_ => secondOffset += 3);

            using var server = TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files",
#pragma warning disable CS0618 // Type or member is obsolete
                    OnUploadCompleteAsync = (fileId, cbStore, cancellationToken) =>
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        // Check that the store provided is the same as the one in the configuration.
                        cbStore.ShouldBeSameAs(store);
                        cancellationToken.ShouldNotBe(default);

                        onUploadCompleteCallCounts.TryGetValue(fileId, out int count);

                        count++;
                        onUploadCompleteCallCounts[fileId] = count;
                        return Task.FromResult(true);
                    },
                    Events = new Events
                    {
                        OnFileCompleteAsync = ctx =>
                        {
                            // Check that the store provided is the same as the one in the configuration.
                            ctx.Store.ShouldBeSameAs(store);
                            ctx.CancellationToken.ShouldNotBe(default);

                            onFileCompleteAsyncCallbackCounts.TryGetValue(
                                ctx.FileId,
                                out int count
                            );

                            count++;
                            onFileCompleteAsyncCallbackCounts[ctx.FileId] = count;
                            return Task.FromResult(true);
                        },
                    },
                }
            );

            var response1 = await server
                .CreateTusResumableRequest("/files/file1")
                .AddHeader("Upload-Offset", "3")
                .AddBody()
                .SendAsync("PATCH");

            response1.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            var response2 = await server
                .CreateTusResumableRequest("/files/file2")
                .AddHeader("Upload-Offset", "2")
                .AddBody()
                .SendAsync("PATCH");

            response2.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // File is already complete, make sure it does not run OnUploadComplete twice.
            response1 = await server
                .CreateTusResumableRequest("/files/file1")
                .AddHeader("Upload-Offset", "6")
                .AddBody()
                .SendAsync("PATCH");

            response1.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            onFileCompleteAsyncCallbackCounts["file1"].ShouldBe(1);
            onFileCompleteAsyncCallbackCounts["file2"].ShouldBe(0);
        }

        [Fact]
        public async Task Does_Not_Run_OnFileCompleteAsync_Nor_OnUploadCompleteAsync_If_Upload_Length_Is_Zero()
        {
            // Note: The PATCH request should not happen if an empty file was created. The POST request is responsible for firing the OnFileCompleteAsync event so make sure that the PATCH does not also fire it.

            // Old callback handler
            var onUploadCompleteCallCounts = 0;

            // New event handler
            var onFileCompleteAsyncCallbackCounts = 0;

            var store = Substitute.For<ITusStore>().WithExistingFile("file1", _ => 0, _ => 0);

            using var server = TestServerFactory.Create(
                new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files",
#pragma warning disable CS0618 // Type or member is obsolete
                    OnUploadCompleteAsync = (__, ___, ____) =>
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        onUploadCompleteCallCounts++;
                        return Task.FromResult(true);
                    },
                    Events = new Events
                    {
                        OnFileCompleteAsync = __ =>
                        {
                            onFileCompleteAsyncCallbackCounts++;
                            return Task.FromResult(true);
                        },
                    },
                }
            );

            var response = await server
                .CreateTusResumableRequest("/files/file1")
                .AddHeader("Upload-Offset", "0")
                .AddBody()
                .SendAsync("PATCH");

            await response.ShouldBeErrorResponse(
                HttpStatusCode.BadRequest,
                "Upload is already complete."
            );

            onUploadCompleteCallCounts.ShouldBe(0);
            onFileCompleteAsyncCallbackCounts.ShouldBe(0);
        }

        [Fact]
        public async Task OnAuthorized_Is_Called()
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(5);

            using var server = TestServerFactory.Create(
                store,
                new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        _onAuthorizeWasCalled = true;
                        _onAuthorizeWasCalledWithIntent = ctx.Intent;
                        return Task.FromResult(0);
                    },
                }
            );
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            _onAuthorizeWasCalled.ShouldBeTrue();
            _onAuthorizeWasCalledWithIntent.ShouldBe(IntentType.WriteFile);
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnAuthorized_Fails_The_Request()
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(5);

            using var server = TestServerFactory.Create(
                store,
                new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        ctx.FailRequest(HttpStatusCode.Unauthorized);
                        return Task.FromResult(0);
                    },
                }
            );
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.ShouldNotContainHeaders("Tus-Resumable", "Upload-Offset", "Upload-Expires");
        }

        [Fact]
        public async Task OnBeforeWrite_Is_Called()
        {
            var onBeforeWriteWasCalled = false;
            var onBeforeWriteUploadOffset = -1L;
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(5);

            using var server = TestServerFactory.Create(
                store,
                new Events
                {
                    OnBeforeWriteAsync = ctx =>
                    {
                        onBeforeWriteWasCalled = true;
                        onBeforeWriteUploadOffset = ctx.UploadOffset;
                        return Task.FromResult(0);
                    },
                }
            );
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            onBeforeWriteWasCalled.ShouldBeTrue();
            onBeforeWriteUploadOffset.ShouldBe(5);
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnBeforeWrite_Fails_The_Request()
        {
            var store = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: 10, uploadOffset: 5);
            store
                .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(5);

            using var server = TestServerFactory.Create(
                store,
                new Events
                {
                    OnBeforeWriteAsync = ctx =>
                    {
                        ctx.FailRequest(HttpStatusCode.BadRequest);
                        return Task.FromResult(0);
                    },
                }
            );
            var response = await server
                .CreateTusResumableRequest("/files/testfile")
                .AddHeader("Upload-Offset", "5")
                .AddBody()
                .SendAsync("PATCH");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.ShouldNotContainHeaders("Upload-Offset");
        }

        [Fact]
        public async Task A_TusConfigurationException_Is_Thrown_If_File_Was_Created_Using_UploadDeferLength_But_The_Store_Used_For_Writing_Data_Does_Not_Support_UploadDeferLength()
        {
            var creationStoreWithUploadDeferLength = Substitute.For<
                ITusStore,
                ITusCreationStore,
                ITusCreationDeferLengthStore
            >();
            ((ITusCreationStore)creationStoreWithUploadDeferLength)
                .CreateFileAsync(default, default, default)
                .ReturnsForAnyArgs("testfile");

            var storeWithoutUploadDeferLength = Substitute
                .For<ITusStore>()
                .WithExistingFile("testfile", uploadLength: null, uploadOffset: 0);

            using (var server = TestServerFactory.Create(creationStoreWithUploadDeferLength))
            {
                var response = await server
                    .CreateTusResumableRequest("/files")
                    .AddHeader("Upload-Defer-Length", "1")
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(HttpStatusCode.Created);
            }

            using (var server = TestServerFactory.Create(storeWithoutUploadDeferLength))
            {
                var exception = await Assert.ThrowsAsync<TusConfigurationException>(async () =>
                    await server
                        .CreateTusResumableRequest("/files/testfile")
                        .AddHeader("Upload-Offset", "0")
                        .AddHeader("Upload-Length", "100")
                        .AddBody()
                        .SendAsync("PATCH")
                );

                exception.Message.ShouldBe(
                    $"File testfile does not have an upload length and the current store ({storeWithoutUploadDeferLength.GetType().FullName}) does not support Upload-Defer-Length so no new upload length can be set"
                );
            }
        }

        private void AssertAndResetForwardCall(bool expectedCallForwarded)
        {
            _callForwarded.ShouldBe(expectedCallForwarded);
            _onAuthorizeWasCalled.ShouldBe(!expectedCallForwarded);

            _onAuthorizeWasCalled = false;
            _callForwarded = false;
            _onAuthorizeWasCalledWithIntent = null;
        }
    }
}
