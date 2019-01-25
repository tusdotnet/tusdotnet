using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;
#if netfull
using Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.Builder;
#endif

namespace tusdotnet.test.Tests
{
    public class PatchTests
    {
        private bool _callForwarded;
        private bool _onAuthorizeWasCalled;
        private IntentType? _onAuthorizeWasCalledWithIntent;


        [Fact]
        public async Task Ignores_Request_If_Url_Does_Not_Match()
        {
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files",
                    Events = new Events
                    {
                        OnAuthorizeAsync = ctx =>
                        {
                            _onAuthorizeWasCalled = true;
                            return Task.FromResult(0);
                        }
                    }
                });

                app.Use((ctx, next) =>
                {
                    _callForwarded = true;
                    return Task.FromResult(true);
                });
            }))
            {
                await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("PATCH");
                AssertForwardCall(true);

                await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("PATCH");
                AssertForwardCall(false);

                await server.CreateRequest("/otherfiles/testfile").AddTusResumableHeader().SendAsync("PATCH");
                AssertForwardCall(true);
            }
        }

        [Fact]
        public async Task Returns_404_Not_Found_If_File_Was_Not_Found()
        {
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files"
                });
            }))
            {
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
        }

        [Theory]
        [InlineData("text/plain")]
        [InlineData(null)]
        [InlineData("application/octet-stream")]
        [InlineData("application/json")]
        public async Task Returns_400_Bad_Request_If_An_Incorrect_Content_Type_Is_Provided(string contentType)
        {
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files"
                });
            }))
            {
                var requestBuilder = server
                    .CreateRequest("/files/testfile")
                    .AddHeader("Upload-Offset", "0")
                    .AddTusResumableHeader();

                if (contentType != null)
                {
                    requestBuilder = requestBuilder.And(m => m.AddBody(contentType));
                }

                var response = await requestBuilder.SendAsync("PATCH");

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                    $"Content-Type {contentType} is invalid. Must be application/offset+octet-stream");
                response.ShouldContainTusResumableHeader();
            }
        }

        [Fact]
        public async Task Returns_400_Bad_Request_For_Missing_Upload_Offset_Header()
        {
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .SendAsync("patch");

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Missing Upload-Offset header");
            }
        }

        [Theory]
        [InlineData("uploadoffset", "")]
        [InlineData("1.0.1", "")]
        [InlineData("0.2", "")]
        [InlineData("-100", "Header Upload-Offset must be a positive number")]
        public async Task Returns_400_Bad_Request_For_Invalid_Upload_Offset_Header(string uploadOffset,
            string expectedServerErrorMessage)
        {
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddHeader("Upload-Offset", uploadOffset)
                    .AddTusResumableHeader()
                    .SendAsync("patch");

                await
                    response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                        string.IsNullOrEmpty(expectedServerErrorMessage)
                            ? "Could not parse Upload-Offset header"
                            : expectedServerErrorMessage);
            }
        }

        [Theory]
        [InlineData(5)]
        [InlineData(100)]
        public async Task Returns_409_Conflict_If_Upload_Offset_Does_Not_Match_File_Offset(int offset)
        {
            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore>();
                store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);

                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", offset.ToString())
                    .SendAsync("PATCH");

                await response.ShouldBeErrorResponse(HttpStatusCode.Conflict,
                    $"Offset does not match file. File offset: 10. Request offset: {offset}");
            }
        }

        [Fact]
        public async Task Returns_400_Bad_Request_If_Upload_Is_Already_Complete()
        {
            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore>();
                store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);
                store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);

                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", "10")
                    .SendAsync("PATCH");

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Upload is already complete.");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_204_No_Content_On_Success(string methodToUse)
        {
            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore>();
                store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(5);
                store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);
                store.AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", "5")
                    .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                    .SendAsync(methodToUse);

                response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Response_Contains_The_Correct_Headers_On_Success(string methodToUse)
        {
            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore, ITusCreationDeferLengthStore>();
                store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(5);
                store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);
                store.AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", "5")
                    .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                    .SendAsync(methodToUse);

                response.ShouldContainTusResumableHeader();
                response.ShouldContainHeader("Upload-Offset", "10");
                response.Headers.Contains("Upload-Expires").ShouldBeFalse();
            }
        }

        [Fact]
        public async Task Returns_Store_Exceptions_As_400_Bad_Request()
        {
            // This test does not work properly using the OWIN TestServer.
            // It will always throw an exception instead of returning the proper error message to the client.
            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore>();
                store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(5);
                store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);
                store.AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                    .Throws(new TusStoreException("Test exception"));

                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                var requestBuilder = server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", "5");

                var response = await requestBuilder.SendAsync("PATCH");
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
                response.Content.ReadAsStringAsync().Result.ShouldBe("Test exception");
            }
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
            var store = Substitute.For<ITusStore>();
            store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(5);
            store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);

            store.AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Throws(
                    info =>
                    {
                        if (pipelineDetails.FlagsCancellationTokenAsCancelled)
                        {
                            cts.Cancel();
                        }
                        throw pipelineDetails.ExceptionThatIsThrown;
                    });

            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var responseStatus = HttpStatusCode.OK;
            var response = new ResponseAdapter
            {
                Body = new MemoryStream(),
                SetHeader = (key, value) => responseHeaders[key] = value,
                SetStatus = status => responseStatus = status
            };

            var context = new ContextAdapter
            {
                CancellationToken = cts.Token,
                Configuration = new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store
                },
                Request = new RequestAdapter("/files")
                {
                    Headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"Content-Type", new List<string>(1) {"application/offset+octet-stream"}},
                        {"Tus-Resumable", new List<string>(1) {"1.0.0"}},
                        {"Upload-Offset", new List<string>(1) {"5"}}
                    },
                    Method = "PATCH",
                    Body = new MemoryStream(new byte[3]),
                    RequestUri = new Uri("https://localhost:8080/files/testfile")
                },
                Response = response
            };

            var handled = await TusProtocolHandlerIntentBased.Invoke(context);

            handled.ShouldBe(ResultType.StopExecution);
            responseStatus.ShouldBe(HttpStatusCode.OK);
            responseHeaders.Count.ShouldBe(0);
            response.Body.Length.ShouldBe(0);
        }

        [Fact]
        public async Task Returns_409_Conflict_If_Multiple_Requests_Try_To_Patch_The_Same_File()
        {
            using (var server = TestServerFactory.Create(app =>
            {
                var random = new Random();
                var offset = 5;
                var store = Substitute.For<ITusStore>();
                store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
                store
                    .GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>())
                    .Returns(offset);
                store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);
                store
                    .AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                    .Returns(info =>
                    {
                        // Emulate some latency in the request.
                        Thread.Sleep(random.Next(100, 301));
                        offset += 3;
                        return 3;
                    });

                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files"
                });
            }))
            {
                // Duplicated code due to: 
                // "System.InvalidOperationException: The request message was already sent. Cannot send the same request message multiple times."
                var task1 = server
                    .CreateRequest("/files/testfile")
                    .And(message =>
                    {
                        message.Content = new StreamContent(new MemoryStream(new byte[3]));
                        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
                    })
                    .AddHeader("Upload-Offset", "5")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");
                var task2 = server
                    .CreateRequest("/files/testfile")
                    .And(message =>
                    {
                        message.Content = new StreamContent(new MemoryStream(new byte[3]));
                        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
                    })
                    .AddHeader("Upload-Offset", "5")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");

                await Task.WhenAll(task1, task2);

                if (task1.Result.StatusCode == HttpStatusCode.NoContent)
                {
                    task1.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                    task2.Result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
                }
                else
                {
                    task1.Result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
                    task2.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                }
            }
        }

        [Fact]
        public async Task Runs_OnFileCompleteAsync_And_OnUploadCompleteAsync_When_Upload_Is_Complete()
        {
            // Old callback handler
            var onUploadCompleteCallCounts = new Dictionary<string, int>(2)
            {
                {"file1", 0},
                {"file2", 0}
            };

            // New event handler
            var onFileCompleteAsyncCallbackCounts = new Dictionary<string, int>(2)
            {
                {"file1", 0},
                {"file2", 0}
            };
            var firstOffset = 3;
            var secondOffset = 2;

            using (var server = TestServerFactory.Create(app =>
            {
                var store = Substitute.For<ITusStore>();
                store.FileExistAsync("file1", Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadLengthAsync("file1", Arg.Any<CancellationToken>()).Returns(6);
                store.GetUploadOffsetAsync("file1", Arg.Any<CancellationToken>()).Returns(info => firstOffset);
                store.AppendDataAsync("file1", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                    .Returns(3)
                    .AndDoes(info => firstOffset += 3);

                store.FileExistAsync("file2", Arg.Any<CancellationToken>()).Returns(true);
                store.GetUploadLengthAsync("file2", Arg.Any<CancellationToken>()).Returns(6);
                store.GetUploadOffsetAsync("file2", Arg.Any<CancellationToken>()).Returns(info => secondOffset);
                store.AppendDataAsync("file2", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                    .Returns(3)
                    .AndDoes(info => secondOffset += 3);

                app.UseTus(request => new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files",
#pragma warning disable CS0618 // Type or member is obsolete
                    OnUploadCompleteAsync = (fileId, cbStore, cancellationToken) =>
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        // Check that the store provided is the same as the one in the configuration.
                        cbStore.ShouldBeSameAs(store);
                        cancellationToken.ShouldNotBe(default(CancellationToken));

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
                            ctx.CancellationToken.ShouldNotBe(default(CancellationToken));

                            onFileCompleteAsyncCallbackCounts.TryGetValue(ctx.FileId, out int count);

                            count++;
                            onFileCompleteAsyncCallbackCounts[ctx.FileId] = count;
                            return Task.FromResult(true);
                        }
                    }
                });
            }))
            {
                var response1 = await server
                    .CreateRequest("/files/file1")
                    .And(m => m.AddBody())
                    .AddHeader("Upload-Offset", "3")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");

                response1.StatusCode.ShouldBe(HttpStatusCode.NoContent);

                var response2 = await server
                    .CreateRequest("/files/file2")
                    .And(m => m.AddBody())
                    .AddHeader("Upload-Offset", "2")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");

                response2.StatusCode.ShouldBe(HttpStatusCode.NoContent);

                // File is already complete, make sure it does not run OnUploadComplete twice.
                response1 = await server
                    .CreateRequest("/files/file1")
                    .And(m => m.AddBody())
                    .AddHeader("Upload-Offset", "6")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");

                response1.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

                onFileCompleteAsyncCallbackCounts["file1"].ShouldBe(1);
                onFileCompleteAsyncCallbackCounts["file2"].ShouldBe(0);
            }
        }

        [Fact]
        public async Task OnAuthorized_Is_Called()
        {
            var store = Substitute.For<ITusStore>();
            store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(5);
            store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);
            store.AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

            using (var server = TestServerFactory.Create(store, new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    _onAuthorizeWasCalled = true;
                    _onAuthorizeWasCalledWithIntent = ctx.Intent;
                    return Task.FromResult(0);
                }
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", "5")
                    .SendAsync("PATCH");

                response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

                _onAuthorizeWasCalled.ShouldBeTrue();
                _onAuthorizeWasCalledWithIntent.ShouldBe(IntentType.WriteFile);
            }
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnAuthorized_Fails_The_Request()
        {
            var store = Substitute.For<ITusStore>();
            store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(5);
            store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(10);
            store.AppendDataAsync("testfile", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

            using (var server = TestServerFactory.Create(store, new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.FromResult(0);
                }
            }))
            {
                var response = await server
                    .CreateRequest("/files/testfile")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", "5")
                    .SendAsync("PATCH");

                response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
                response.ShouldNotContainHeaders("Tus-Resumable", "Upload-Offset", "Upload-Expires");
            }
        }

        private void AssertForwardCall(bool expectedCallForwarded)
        {
            _callForwarded.ShouldBe(expectedCallForwarded);
            _onAuthorizeWasCalled.ShouldBe(!expectedCallForwarded);

            _onAuthorizeWasCalled = false;
            _callForwarded = false;
            _onAuthorizeWasCalledWithIntent = null;
        }
    }
}
