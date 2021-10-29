using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;
using tusdotnet.Models.Configuration;
#if netfull
using Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.Builder;
#endif

namespace tusdotnet.test.Tests
{
    public class DeleteTests
    {
        private bool _callForwarded;
        private bool _onAuthorizeWasCalled;
        private IntentType? _onAuthorizeWasCalledWithIntent;

        [Theory, XHttpMethodOverrideData]
        public async Task Forwards_Calls_If_The_Store_Does_Not_Support_Termination(string methodToUse)
        {
            using var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => GetConfigForEmptyStoreWithOnAuthorize(storeSupportsTermination: false));

                app.Run(ctx =>
                {
                    _callForwarded = true;
                    return Task.FromResult(true);
                });
            });

            await server
                .CreateRequest("/files/testfiledelete")
                .AddHeader("Tus-Resumable", "1.0.0")
                .OverrideHttpMethodIfNeeded("DELETE", methodToUse)
                .SendAsync(methodToUse);

            AssertForwardCall(true);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Ignores_Request_If_Url_Does_Not_Match(string methodToUse)
        {
            using var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => GetConfigForEmptyStoreWithOnAuthorize(storeSupportsTermination: true));

                app.Run(ctx =>
                {
                    _callForwarded = true;
                    return Task.FromResult(true);
                });
            });

            await server.CreateRequest("/files").AddTusResumableHeader().OverrideHttpMethodIfNeeded("DELETE", methodToUse).SendAsync(methodToUse);
            AssertForwardCall(true);

            await server.CreateRequest("/files/testfiledelete").AddTusResumableHeader().OverrideHttpMethodIfNeeded("DELETE", methodToUse).SendAsync(methodToUse);
            AssertForwardCall(false);

            await server.CreateRequest("/otherfiles/testfiledelete").AddTusResumableHeader().OverrideHttpMethodIfNeeded("DELETE", methodToUse).SendAsync(methodToUse);
            AssertForwardCall(true);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_204_No_Content_On_Success(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusTerminationStore>();
            store.FileExistAsync("testfiledelete", Arg.Any<CancellationToken>()).Returns(true);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateRequest("/files/testfiledelete")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("DELETE", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.ShouldContainTusResumableHeader();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_409_Conflict_If_Multiple_Requests_Try_To_Delete_The_Same_File(string methodToUse)
        {
            var random = new Random();
            var store = Substitute.For<ITusStore, ITusTerminationStore>();
            var terminationStore = (ITusTerminationStore)store;
            store.FileExistAsync("testfiledelete", Arg.Any<CancellationToken>()).Returns(true);
            terminationStore
                .DeleteFileAsync("testfiledelete", Arg.Any<CancellationToken>())
                .Returns(info =>
                {
                    // Emulate some latency in the request.
                    Thread.Sleep(random.Next(100, 301));
                    return Task.FromResult(0);
                });

            using var server = TestServerFactory.Create(store);

            var task1 = server
                .CreateRequest("/files/testfiledelete")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("DELETE", methodToUse)
                .SendAsync(methodToUse);
            var task2 = server
                .CreateRequest("/files/testfiledelete")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("DELETE", methodToUse)
                .SendAsync(methodToUse);

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

        [Theory, XHttpMethodOverrideData]
        public async Task Runs_OnBeforeDeleteAsync_Before_Deleting_The_File(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusTerminationStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);

            var beforeDeleteCalled = false;

            var terminationStore = (ITusTerminationStore)store;
            terminationStore.DeleteFileAsync(null, CancellationToken.None)
                .ReturnsForAnyArgs(Task.FromResult(0))
                .AndDoes(ci => beforeDeleteCalled.ShouldBeTrue());

            var events = new Events
            {
                OnBeforeDeleteAsync = context =>
                {
                    beforeDeleteCalled = true;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            var response = await server
                .CreateRequest("/files/testfiledelete")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("DELETE", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            beforeDeleteCalled.ShouldBeTrue();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_BadRequest_If_OnBeforeDelete_Fails_The_Request(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusTerminationStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);

            var terminationStore = (ITusTerminationStore)store;
            terminationStore.DeleteFileAsync(null, CancellationToken.None).ReturnsForAnyArgs(Task.FromResult(0));

            var events = new Events
            {
                OnBeforeDeleteAsync = context =>
                {
                    context.FailRequest("Cannot delete file");
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            var response = await server
                .CreateRequest("/files/testfiledelete")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("DELETE", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Cannot delete file");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Runs_OnDeleteCompleteAsync_After_Deleting_The_File(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();
            var store = Substitute.For<ITusStore, ITusTerminationStore>();
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);

            var onDeleteCompleteAsyncCalled = false;
            var deleteFileAsyncCalled = false;
            string callbackFileId = null;
            ITusStore callbackStore = null;

            var terminationStore = (ITusTerminationStore)store;
            terminationStore.DeleteFileAsync(null, CancellationToken.None)
                .ReturnsForAnyArgs(Task.FromResult(0))
                .AndDoes(ci =>
                {
                    onDeleteCompleteAsyncCalled.ShouldBeFalse();
                    deleteFileAsyncCalled = true;
                });

            var events = new Events
            {
                OnDeleteCompleteAsync = ctx =>
                {
                    deleteFileAsyncCalled.ShouldBe(true);
                    onDeleteCompleteAsyncCalled = true;
                    callbackFileId = ctx.FileId;
                    callbackStore = ctx.Store;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            var response = await server
                .CreateRequest($"/files/{fileId}")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("DELETE", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            deleteFileAsyncCalled.ShouldBeTrue();
            callbackFileId.ShouldBe(fileId);
            callbackStore.ShouldBe(store);
        }

        [Fact]
        public async Task OnAuthorized_Is_Called()
        {
            using var server = TestServerFactory.Create(GetConfigForEmptyStoreWithOnAuthorize(storeSupportsTermination: true));

            var response = await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("DELETE");

            response.ShouldContainTusResumableHeader();
            _onAuthorizeWasCalled.ShouldBeTrue();
            _onAuthorizeWasCalledWithIntent.ShouldBe(IntentType.DeleteFile);
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnAuthorized_Fails_The_Request()
        {
            using var server = TestServerFactory.Create(GetConfigForEmptyStoreWithOnAuthorize(storeSupportsTermination: true).Store, new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.FromResult(0);
                }
            });

            var response = await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("DELETE");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.ShouldNotContainHeaders("Tus-Resumable", "Content-Type");
        }

        private DefaultTusConfiguration GetConfigForEmptyStoreWithOnAuthorize(bool storeSupportsTermination)
        {
            ITusStore store = null;
            if (storeSupportsTermination)
            {
                store = Substitute.For<ITusStore, ITusTerminationStore>();
            }
            else
            {
                store = Substitute.For<ITusStore>();
            }

            return new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files",
                Events = new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        _onAuthorizeWasCalled = true;
                        _onAuthorizeWasCalledWithIntent = ctx.Intent;
                        return Task.FromResult(0);
                    }
                }
            };
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