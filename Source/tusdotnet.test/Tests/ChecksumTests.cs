using System.IO;
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
using System.Collections.Generic;
using tusdotnet.Adapters;
using Microsoft.AspNetCore.Http;
using System;
#if netfull
using Owin;
#endif

namespace tusdotnet.test.Tests
{
    public class ChecksumTests
    {
        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Checksum_Algorithm_Is_Not_Supported(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();

            var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
            store = store.WithExistingFile(fileId, 10, 5);
            store.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

            var checksumStore = (ITusChecksumStore)store;
            checksumStore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "md5" });

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/" + fileId)
                .AddHeader("Upload-Offset", "5")
                .AddHeader("Upload-Checksum", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=")
                .AddBody()
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Unsupported checksum algorithm. Supported algorithms are: md5");
            response.ShouldContainTusResumableHeader();

            await store.DidNotReceive().FileExistAsync(null, CancellationToken.None);
            await store.DidNotReceive().GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>());
            await store.DidNotReceive().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_460_Checksum_Mismatch_If_The_Checksum_Does_Not_Match(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();

            var store = Substitute.For<ITusStore, ITusChecksumStore>();
            store = store.WithExistingFile(fileId, 10, 5);
            store.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

            var checksumStore = (ITusChecksumStore)store;
            checksumStore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });
            checksumStore.VerifyChecksumAsync(null, null, null, CancellationToken.None).ReturnsForAnyArgs(false);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/" + fileId)
                .AddHeader("Upload-Offset", "5")
                .AddHeader("Upload-Checksum", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=")
                .AddBody()
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .SendAsync(methodToUse);

            await response.ShouldBeErrorResponse((HttpStatusCode)460, "Header Upload-Checksum does not match the checksum of the file");
            response.ShouldContainTusResumableHeader();
        }

        [Fact]
        public async Task Checksum_Is_Verified_If_Client_Disconnects()
        {
            var cts = new CancellationTokenSource();

            var fileId = Guid.NewGuid().ToString();

            var store = Substitute.For<ITusStore, ITusChecksumStore>();
            store = store.WithExistingFile(fileId, 10, 5);
            // Cancel token source to emulate a client disconnect
            store.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5).AndDoes(_ => cts.Cancel());

            var checksumStore = (ITusChecksumStore)store;
            checksumStore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });
            checksumStore.VerifyChecksumAsync(null, null, null, CancellationToken.None).ReturnsForAnyArgs(false);

            var responseStatusCode = HttpStatusCode.OK;
            var responseStream = new MemoryStream();

            await TusProtocolHandlerIntentBased.Invoke(new ContextAdapter
            {
                CancellationToken = cts.Token,
                Configuration = new DefaultTusConfiguration
                {
                    Store = store,
                    UrlPath = "/files",
                },
                Request = new RequestAdapter("/files")
                {
                    Body = new MemoryStream(),
                    RequestUri = new Uri("https://localhost/files/" + fileId),
                    Headers = new Dictionary<string, List<string>>
                    {
                        { Constants.HeaderConstants.TusResumable, new List<string> { Constants.HeaderConstants.TusResumableValue } },
                        // Just random gibberish as checksum
                        { Constants.HeaderConstants.UploadChecksum, new List<string> { "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=" } },
                        { Constants.HeaderConstants.UploadOffset, new List<string> { "5" } },
                        { Constants.HeaderConstants.ContentType, new List<string> { "application/offset+octet-stream" } },
                    },
                    Method = "PATCH"
                },
                Response = new ResponseAdapter
                {
                    Body = responseStream,
                    SetHeader = (_, __) => { },
                    SetStatus = status => responseStatusCode = status
                },
                HttpContext = new DefaultHttpContext()
            });

            await checksumStore.ReceivedWithAnyArgs().VerifyChecksumAsync(null, null, null, CancellationToken.None);

            responseStatusCode.ShouldBe((HttpStatusCode)460);

            responseStream.Seek(0, SeekOrigin.Begin);
            using var streamReader = new StreamReader(responseStream);
            streamReader.ReadToEnd().ShouldBe("Header Upload-Checksum does not match the checksum of the file");
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_204_No_Content_If_Checksum_Matches(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();

            var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
            store = store.WithExistingFile(fileId, 10, 5);
            store.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);
            var checksumStore = (ITusChecksumStore)store;
            checksumStore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });
            checksumStore.VerifyChecksumAsync(null, null, null, CancellationToken.None).ReturnsForAnyArgs(true);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateTusResumableRequest("/files/" + fileId)
                .AddHeader("Upload-Offset", "5")
                .AddHeader("Upload-Checksum", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=")
                .AddBody()
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.ShouldContainTusResumableHeader();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_204_No_Content_If_Store_Supports_Checksum_But_No_Checksum_Is_Provided(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();

            var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
            store.FileExistAsync(fileId, CancellationToken.None).ReturnsForAnyArgs(true);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(5);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(10);
            store.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);
            var cstore = (ITusChecksumStore)store;
            cstore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });
            cstore.VerifyChecksumAsync(null, null, null, CancellationToken.None).ReturnsForAnyArgs(true);

            using var server = TestServerFactory.Create(store);

            var response = await server
                .CreateRequest("/files/" + fileId)
                .And(m => m.AddBody())
                .AddTusResumableHeader()
                .AddHeader("Upload-Offset", "5")
                .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.ShouldContainTusResumableHeader();
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_400_Bad_Request_If_Upload_Checksum_Header_Is_Unparsable(string methodToUse)
        {
            var fileId = Guid.NewGuid().ToString();

            var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
            store = store.WithExistingFile(fileId, 10, 5);
            store.AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

            var checksumStore = (ITusChecksumStore)store;
            checksumStore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "md5" });

            using var server = TestServerFactory.Create(store);

            foreach (var unparsables in new[] { "Kq5sNclPz7QV2+lfQIuc6R7oRu0=", "sha1 ", "", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0" })
            {
#if netstandard
                // ASP.NET Core ignores empty headers so there is no way of knowing if the header was sent empty
                // or if the header is simply absent

                if (unparsables?.Length == 0)
                {
                    continue;
                }
#endif
                var response = await server
                    .CreateTusResumableRequest("/files/" + fileId)
                    .AddHeader("Upload-Offset", "5")
                    .AddHeader("Upload-Checksum", unparsables)
                    .AddBody()
                    .OverrideHttpMethodIfNeeded("PATCH", methodToUse)
                    .SendAsync(methodToUse);

                await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Could not parse Upload-Checksum header");
                response.ShouldContainTusResumableHeader();
            }

            await store.DidNotReceive().GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>());
            await store.DidNotReceive().AppendDataAsync(fileId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            await checksumStore.DidNotReceive().GetSupportedAlgorithmsAsync(Arg.Any<CancellationToken>());
        }
    }
}
