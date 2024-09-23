#if trailingheaders

using System;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Builder;
using tusdotnet.test.Extensions;
using NSubstitute;
using tusdotnet.Interfaces;
using System.Threading.Tasks;
using Xunit;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using System.Net;
using tusdotnet.Adapters;
using System.Collections.Generic;
using tusdotnet.Models;
using System.Linq;
using System.Linq.Expressions;
using tusdotnet.test.Helpers;

namespace tusdotnet.test.Tests
{
    public class ChecksumTrailerTests
    {
        [Fact]
        public async Task Returns_204_No_Content_If_ChecksumTrailer_Extension_Is_Disabled()
        {
            var store = CreateStore<ITusChecksumStore>();

            using var server = CreateTestServerWithChecksumTrailer(store, "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=", allowedExtensions: TusExtensions.All.Except(TusExtensions.ChecksumTrailer));

            var response = await server
                .CreateTusResumableRequest("/files/checksum")
                .AddHeader("Upload-Offset", "5")
                .DeclareTrailingChecksumHeader()
                .AddBody()
                .SendAsync("patch");

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            await store.DidNotReceiveWithAnyArgs().VerifyChecksumAsync(default, default, default, default);
        }

        [Fact]
        public async Task Returns_204_No_Content_If_Checksum_Matches_Using_Trailing_Header()
        {
            var store = CreateStore<ITusChecksumStore>();

            using var server = CreateTestServerWithChecksumTrailer(store, "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=");

            var response = await server
                .CreateTusResumableRequest("/files/checksum")
                .AddHeader("Upload-Offset", "5")
                .DeclareTrailingChecksumHeader()
                .AddBody()
                .SendAsync("patch");

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            await store.Received().VerifyChecksumAsync("checksum", "sha1", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Returns_400_Bad_Request_If_Checksum_Algorithm_Is_Not_Supported_Using_Trailing_Header()
        {
            var store = CreateStore<ITusChecksumStore>("md5", false);

            using var server = CreateTestServerWithChecksumTrailer(store, "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=");

            var response = await server
               .CreateTusResumableRequest("/files/checksum")
               .AddHeader("Upload-Offset", "5")
               .DeclareTrailingChecksumHeader()
               .AddBody()
               .SendAsync("patch");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            await ((ITusStore)store).Received().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());

            // Note: Verify that store was still called with sha1 as algoritm and an empty checksum to force the store to trigger a discard of data.
            await store.Received().VerifyChecksumAsync("checksum", "sha1", Arg.Is(GetFallbackChecksumPredicate()), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("Kq5sNclPz7QV2+lfQIuc6R7oRu0=")]
        [InlineData("sha1 ")]
        [InlineData("sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0")]
        public async Task Returns_400_Bad_Request_If_Checksum_Is_Unparsable_Using_Trailing_Header(string uploadChecksumTrailingHeader)
        {
            var store = CreateStore<ITusChecksumStore>(verifyChecksumAsyncReturnValue: false);

            using var server = CreateTestServerWithChecksumTrailer(store, uploadChecksumTrailingHeader);

            var response = await server
               .CreateTusResumableRequest("/files/checksum")
               .AddHeader("Upload-Offset", "5")
               .DeclareTrailingChecksumHeader()
               .AddBody()
               .SendAsync("patch");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            await ((ITusStore)store).Received().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());

            // Note: Verify that store was still called with sha1 as algoritm and an empty checksum to force the store to trigger a discard of data.
            await store.Received().VerifyChecksumAsync("checksum", "sha1", Arg.Is(GetFallbackChecksumPredicate()), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Returns_460_Checksum_Mismatch_If_The_Checksum_Does_Not_Match_Using_Trailing_Header()
        {
            var checksumStore = CreateStore<ITusChecksumStore>(verifyChecksumAsyncReturnValue: false);
            using var server = CreateTestServerWithChecksumTrailer(checksumStore, "sha1 Kq5sNclPz7QV2+lfQIuc6R7o000=");

            var response = await server
               .CreateTusResumableRequest("/files/checksum")
               .AddHeader("Upload-Offset", "5")
               .DeclareTrailingChecksumHeader()
               .AddBody()
               .SendAsync("patch");

            response.StatusCode.ShouldBe((HttpStatusCode)460);

            await ((ITusStore)checksumStore).Received().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());

            await checksumStore.Received().VerifyChecksumAsync("checksum", "sha1", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Returns_400_Bad_Request_If_Both_Leading_And_Trailing_Headers_Are_Used()
        {
            var store = CreateStore<ITusChecksumStore>();
            using var server = CreateTestServerWithChecksumTrailer(store, "sha1 Kq5sNclPz7QV2+lfQIuc6R7o000=");

            var response = await server
              .CreateTusResumableRequest("/files/checksum")
              .AddHeader("Upload-Offset", "5")
              .AddHeader("Upload-Checksum", "sha1 Kq5sNclPz7QV2+lfQIuc6R7o000=")
              .DeclareTrailingChecksumHeader()
              .AddBody()
              .SendAsync("patch");

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Headers Upload-Checksum and trailing header Upload-Checksum are mutually exclusive and cannot be used in the same request");

            await ((ITusStore)store).DidNotReceiveWithAnyArgs().AppendDataAsync(default, default, default);
            await store.DidNotReceiveWithAnyArgs().VerifyChecksumAsync(default, default, default, default);
        }

        [Fact]
        public async Task Returns_400_Bad_Request_If_Trailing_Checksum_Is_Declared_But_Http_Request_Does_Not_Support_Trailing_Headers()
        {
            var store = CreateStore<ITusChecksumStore>();
            using var server = TestServerFactory.Create((ITusStore)store);

            var response = await server
              .CreateTusResumableRequest("/files/checksum")
              .AddHeader("Upload-Offset", "5")
              .DeclareTrailingChecksumHeader()
              .AddBody()
              .SendAsync("patch");

            await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Trailing header Upload-Checksum has been specified but http request does not support trailing headers");

            await ((ITusStore)store).DidNotReceiveWithAnyArgs().AppendDataAsync(default, default, default);
            await store.DidNotReceiveWithAnyArgs().VerifyChecksumAsync(default, default, default, default);
        }

        [Fact]
        public async Task Checksum_Is_Verified_If_Client_Disconnects_Using_Trailing_Header()
        {
            var cts = new CancellationTokenSource();

            var store = CreateStore<ITusStore>(verifyChecksumAsyncReturnValue: false);
            store.AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5).AndDoes(_ => cts.Cancel());

            var httpContext = new DefaultHttpContext
            {
                RequestAborted = cts.Token
            };

            var trailersFeature = Substitute.For<IHttpRequestTrailersFeature>();
            trailersFeature.Available.ReturnsForAnyArgs(false);

            httpContext.Features.Set(trailersFeature);
            httpContext.Request.Headers.Append("Trailer", Constants.HeaderConstants.UploadChecksum);

            var request = new RequestAdapter()
            {
                Body = new MemoryStream(),
                RequestUri = new Uri("https://localhost/files/checksum"),
                Headers = RequestHeaders.FromDictionary(new Dictionary<string, string>
                    {
                        { Constants.HeaderConstants.TusResumable, Constants.HeaderConstants.TusResumableValue},
                        { Constants.HeaderConstants.UploadOffset, "5"},
                        { Constants.HeaderConstants.ContentType, "application/offset+octet-stream"},
                    }),
                Method = "PATCH"
            };
            var config = new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files",
            };
            var context = new ContextAdapter("/files", requestPathBase: null, MiddlewareUrlHelper.Instance, request, config, httpContext);

            await TusV1EventRunner.Invoke(context);

            await store.Received().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());

            // Note: Verify that store was still called with sha1 as algoritm and an empty checksum to force the store to trigger a discard of data.
            await ((ITusChecksumStore)store).Received().VerifyChecksumAsync("checksum", "sha1", Arg.Is(GetFallbackChecksumPredicate()), Arg.Any<CancellationToken>());

            byte[] checksumArgument = (byte[])((ITusChecksumStore)store).ReceivedCalls().First(x => x.GetMethodInfo().Name == nameof(ITusChecksumStore.VerifyChecksumAsync)).GetArguments()[2];

            checksumArgument.Length.ShouldBe(20);
            checksumArgument.ShouldAllBe(b => b == 0);

            context.Response.Status.ShouldBe((HttpStatusCode)460);
            context.Response.Message.ShouldBe("Header Upload-Checksum does not match the checksum of the file");
        }

        private static TestServer CreateTestServerWithChecksumTrailer(ITusChecksumStore store, string trailingUploadChecksumValue, TusExtensions allowedExtensions = null)
        {
            return TestServerFactory.Create(app =>
            {
                app.Use((httpContext, next) =>
                {
                    var trailersFeature = Substitute.For<IHttpRequestTrailersFeature>();

                    var trailers = new HeaderDictionary
                    {
                        { "Upload-Checksum", trailingUploadChecksumValue }
                    };
                    trailersFeature.Available.ReturnsForAnyArgs(true);
                    trailersFeature.Trailers.ReturnsForAnyArgs(trailers);

                    httpContext.Features.Set(trailersFeature);

                    return next();
                });

                var config = new DefaultTusConfiguration()
                {
                    Store = (ITusStore)store,
                    FileLockProvider = new TestServerInMemoryFileLockProvider(),
                    UrlPath = "/files",
                    AllowedExtensions = allowedExtensions ?? TusExtensions.All
                };

#if NET6_0_OR_GREATER

                app.UseRouting();
                app.UseEndpoints(e =>
                {
                    var urlPath = config.UrlPath;
                    config.UrlPath = null;
                    e.MapTus(urlPath, _ => Task.FromResult(config));
                });
#else

                app.UseTus(_ => config);

#endif
            });
        }

        private static T CreateStore<T>(string supportedAlgorithm = "sha1", bool verifyChecksumAsyncReturnValue = true)
        {
            var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
            store = store.WithExistingFile("checksum", 10, 5);
            store.AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

            var checksumStore = (ITusChecksumStore)store;
            checksumStore.GetSupportedAlgorithmsAsync(default).ReturnsForAnyArgs(new[] { supportedAlgorithm });
            checksumStore.VerifyChecksumAsync(default, default, default, default).ReturnsForAnyArgs(verifyChecksumAsyncReturnValue);

            return (T)store;
        }

        private static Expression<Predicate<byte[]>> GetFallbackChecksumPredicate()
        {
            return b => b.Length == 20 && b.All(x => x == 0);
        }
    }
}

#endif