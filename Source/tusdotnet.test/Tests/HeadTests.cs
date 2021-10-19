using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Stores;
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
    public class HeadTests
    {
        private bool _callForwarded;
        private bool _onAuthorizeWasCalled;

        [Fact]
        public async Task Ignores_Request_If_Url_Does_Not_Match()
        {
            using var server = TestServerFactory.Create(app =>
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

                app.Run(ctx =>
                {
                    _callForwarded = true;
                    return Task.FromResult(true);
                });
            });

            await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("HEAD");
            AssertForwardCall(true);

            await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("HEAD");
            AssertForwardCall(false);

            await server.CreateRequest("/otherfiles/testfile").AddTusResumableHeader().SendAsync("HEAD");
            AssertForwardCall(true);
        }

        [Fact]
        public async Task Returns_404_Not_Found_If_File_Was_Not_Found()
        {
            using var server = TestServerFactory.Create(Substitute.For<ITusStore>());

            var response = await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.ShouldContainTusResumableHeader();
        }

        [Fact]
        public async Task Includes_Upload_Length_Header_If_Available()
        {
            var store = Substitute.For<ITusStore>();
            store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
            store.FileExistAsync("otherfile", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(100);
            store.GetUploadLengthAsync("otherfile", Arg.Any<CancellationToken>()).Returns(null as long?);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.ShouldContainHeader("Upload-Length", "100");

            response = await server
                .CreateRequest("/files/otherfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.ShouldNotContainHeaders("Upload-Length");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Response_Contains_The_Correct_Headers_On_Success(string methodToUse)
        {
            using var server = TestServerFactory.Create(CreateMockStore());

            var response = await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .OverrideHttpMethodIfNeeded("HEAD", methodToUse)
                .SendAsync(methodToUse);

            response.ShouldContainTusResumableHeader();
            response.ShouldContainHeader("Upload-Length", "100");
            response.ShouldContainHeader("Upload-Offset", "50");
            response.ShouldContainHeader("Cache-Control", "no-store");
        }

        [Fact]
        public async Task Response_Contains_UploadMetadata_If_Metadata_Exists_For_File()
        {
            // If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata header 
            // and its value as specified by the Client during the creation.

            const string metadata = "filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==,othermeta c29tZSBvdGhlciBkYXRh";

            using var server = TestServerFactory.Create(CreateMockStore(metadata));

            var response = await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.ShouldContainHeader("Upload-Metadata", metadata);
        }

        [Fact]
        public async Task Response_Does_Not_Contain_UploadMetadata_If_Metadata_Does_Not_Exist_For_File()
        {
            // If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata header 
            // and its value as specified by the Client during the creation.

            using var server = TestServerFactory.Create(CreateMockStore());

            var response = await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.Headers.Contains("Upload-Metadata").ShouldBeFalse();
        }

        [Fact]
        public async Task OnAuthorized_Is_Called()
        {
            var onAuthorizeWasCalled = false;
            IntentType? intentProvidedToOnAuthorize = null;

            using var server = TestServerFactory.Create(CreateMockStore(), new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    onAuthorizeWasCalled = true;
                    intentProvidedToOnAuthorize = ctx.Intent;
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.ShouldContainTusResumableHeader();
            response.ShouldContainHeader("Upload-Length", "100");
            response.ShouldContainHeader("Upload-Offset", "50");
            response.ShouldContainHeader("Cache-Control", "no-store");

            onAuthorizeWasCalled.ShouldBeTrue();
            intentProvidedToOnAuthorize.ShouldBe(IntentType.GetFileInfo);
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnAuthorized_Fails_The_Request()
        {
            var store = Substitute.For<ITusStore, ITusCreationStore>();
            using var server = TestServerFactory.Create(store, new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.FromResult(0);
                }
            });

            var response = await server
                .CreateRequest("/files/testfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.ShouldNotContainHeaders(
                "Tus-Resumable",
                "Upload-Length",
                "Upload-Offset",
                "Cache-Control",
                "Content-Type");
        }

        private static ITusStore CreateMockStore(string metadata = null)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore>();
            store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(100);
            store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(50);
            ((ITusCreationStore)store).GetUploadMetadataAsync("testfile", Arg.Any<CancellationToken>()).Returns(metadata);
            return store;
        }

        private void AssertForwardCall(bool expectedCallForwarded)
        {
            _callForwarded.ShouldBe(expectedCallForwarded);
            _onAuthorizeWasCalled.ShouldBe(!expectedCallForwarded);

            _onAuthorizeWasCalled = false;
            _callForwarded = false;
        }
    }
}
