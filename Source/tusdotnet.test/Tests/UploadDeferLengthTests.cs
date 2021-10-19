using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
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
    public class UploadDeferLengthTests
    {
        [Theory, XHttpMethodOverrideData]
        public async Task Forwards_Calls_If_The_Store_Does_Not_Support_Creation(string methodToUse)
        {
            var callForwarded = false;

            var store = Substitute.For<ITusStore, ITusCreationDeferLengthStore>();

            using var server = TestServerFactory.Create(app =>
            {
                app.UseTus(context => new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store
                });

                app.Run(ctx =>
                {
                    callForwarded = true;
                    return Task.FromResult(true);
                });
            });

            await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            callForwarded.ShouldBeTrue();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task File_Is_Created_With_Minus_One_As_UploadLength_If_UploadDeferLength_Is_Set(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainTusResumableHeader();
            response.Headers.Location.ToString().ShouldBe($"/files/{fileId}");

            var createFileAsyncCall = creationStore.GetSingleMethodCall(nameof(creationStore.CreateFileAsync));
            createFileAsyncCall.ShouldNotBeNull();
            var uploadLength = (long)createFileAsyncCall.GetArguments()[0];
            uploadLength.ShouldBe(-1);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task UploadLength_Is_Set_On_Patch_If_UploadDeferLength_Is_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadLengthAsync(null, CancellationToken.None).ReturnsForAnyArgs((long?)null);
            store.GetUploadOffsetAsync(null, CancellationToken.None).ReturnsForAnyArgs(0);
            store.AppendDataAsync(null, null, CancellationToken.None).ReturnsForAnyArgs(3);

            var deferLengthStore = (ITusCreationDeferLengthStore)store;
            deferLengthStore.SetUploadLengthAsync(null, 0, CancellationToken.None)
                .ReturnsForAnyArgs(Task.FromResult(true));

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files/filedeferlength")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .AddHeader("Upload-Offset", "0")
                .AddHeader("Upload-Length", "3")
                .And(message => message.AddBody())
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            var setUploadLengthAsyncCall =
                deferLengthStore.GetSingleMethodCall(nameof(deferLengthStore.SetUploadLengthAsync));

            setUploadLengthAsyncCall.ShouldNotBeNull();
            var uploadLength = (long)setUploadLengthAsyncCall.GetArguments()[1];
            uploadLength.ShouldBe(3);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_BadRequest_If_UploadDeferLength_Is_Not_One(string methodToUse)
        {
            var invalidDeferLengthValues = new[] { "2", Guid.NewGuid().ToString(), "hello world" };

            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();

            using var server = TestServerFactory.Create(store);

            foreach (var deferLength in invalidDeferLengthValues)
            {
                var response = await server.CreateRequest("/files")
                    .AddTusResumableHeader()
                    .OverrideHttpMethodIfNeeded("POST", methodToUse)
                    .AddHeader("Upload-Defer-Length", deferLength)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                    "Header Upload-Defer-Length must have the value '1' or be omitted");
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_BadRequest_If_Both_UploadLength_And_UploadDeferLength_Is_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Length", "1")
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Headers Upload-Length and Upload-Defer-Length are mutually exclusive and cannot be used in the same request");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task UploadDeferLength_Work_With_Partial_Files(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(fileId);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .AddHeader("Upload-Concat", "partial")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.ShouldContainTusResumableHeader();
            response.Headers.Location.ToString().ShouldBe($"/files/{fileId}");

            var createFileAsyncCall = creationStore.GetSingleMethodCall(nameof(creationStore.CreateFileAsync));
            createFileAsyncCall.ShouldNotBeNull();
            var uploadLength = (long)createFileAsyncCall.GetArguments()[0];
            uploadLength.ShouldBe(-1);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Head_Returns_UploadDeferLength_If_No_UploadLength_Has_Been_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadLengthAsync(null, CancellationToken.None).ReturnsForAnyArgs((long?)null);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateRequest($"/files/{Guid.NewGuid()}")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.ShouldContainHeader("Upload-Defer-Length", "1");
            response.Headers.Contains("Upload-Length").ShouldBeFalse();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task UploadLength_Must_Be_Included_In_Patch_Request_If_UploadDeferLength_Has_Been_Set(
            string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadLengthAsync(null, CancellationToken.None).ReturnsForAnyArgs((long?)null);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest($"/files{Guid.NewGuid()}")
                .AddTusResumableHeader()
                .AddHeader("Upload-Offset", "0")
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .And(m => m.AddBody())
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Header Upload-Length must be specified as this file was created using Upload-Defer-Length");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task UploadLength_Must_Not_Be_Changed_Once_Set(string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            store.FileExistAsync(null, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadLengthAsync(null, CancellationToken.None).ReturnsForAnyArgs(100);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest($"/files/{Guid.NewGuid()}")
                .AddTusResumableHeader()
                .AddHeader("Upload-Length", "10")
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .And(m => m.AddBody())
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
                "Upload-Length cannot be updated once set");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnBeforeCreateAsync_Receives_UploadLengthIsDeferred_True_If_UploadDeferLength_Has_Been_Set(
            string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(Guid.NewGuid().ToString());

            bool uploadIsDeferred = false;
            var events = new Events
            {
                OnBeforeCreateAsync = ctx =>
                {
                    uploadIsDeferred = ctx.UploadLengthIsDeferred;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            uploadIsDeferred.ShouldBeTrue();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task OnCreateCompleteAsync_Receives_UploadLengthIsDeferred_True_If_UploadDeferLength_Has_Been_Set(
            string methodToUse)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusCreationDeferLengthStore>();
            var creationStore = (ITusCreationStore)store;
            creationStore.CreateFileAsync(0, null, CancellationToken.None).ReturnsForAnyArgs(Guid.NewGuid().ToString());

            bool uploadIsDeferred = false;
            var events = new Events
            {
                OnCreateCompleteAsync = ctx =>
                {
                    uploadIsDeferred = ctx.UploadLengthIsDeferred;
                    return Task.FromResult(0);
                }
            };

            using var server = TestServerFactory.Create(store, events);

            var response = await server.CreateRequest("/files")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("POST", methodToUse)
                .AddHeader("Upload-Defer-Length", "1")
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            uploadIsDeferred.ShouldBeTrue();
        }
    }
}
