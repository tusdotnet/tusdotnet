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
using System;
#if netfull
using Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.Builder;
#endif

namespace tusdotnet.test.Tests
{
    public class OptionsTests
    {
        private readonly DefaultTusConfiguration _mockTusConfiguration;
        private bool _onAuthorizeWasCalled;
        private IntentType? _onAuthorizeWasCalledWithIntent;
        private bool _callForwarded;

        public OptionsTests()
        {
            var store = (ITusStore)Substitute.For(new[]
            {
                typeof(ITusStore),
                typeof(ITusCreationStore),
                typeof(ITusTerminationStore),
                typeof(ITusChecksumStore),
                typeof(ITusConcatenationStore),
                typeof(ITusExpirationStore),
                typeof(ITusCreationDeferLengthStore)
            }, new object[0]);

            ((ITusChecksumStore)store).GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });

            _mockTusConfiguration = new DefaultTusConfiguration
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

        [Fact]
        public async Task Ignores_Request_If_Url_Does_Not_Match()
        {
            using var server = TestServerFactory.Create(app =>
            {
                app.UseTus(_ => _mockTusConfiguration);

                app.Run(ctx =>
                {
                    _callForwarded = true;
                    return Task.FromResult(true);
                });
            });

            await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS");
            AssertForwardCall(false);

            await server.CreateRequest("/otherfiles").AddTusResumableHeader().SendAsync("OPTIONS");
            AssertForwardCall(true);

            await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("OPTIONS");
            AssertForwardCall(true);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_204_NoContent_On_Success(string methodToUse)
        {
            using var server = TestServerFactory.Create(_mockTusConfiguration);

            var response = await server
                .CreateRequest("/files")
                .OverrideHttpMethodIfNeeded("OPTIONS", methodToUse)
                .SendAsync(methodToUse);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Response_Contains_The_Correct_Headers_On_Success(string methodToUse)
        {
            using (var server = TestServerFactory.Create(_mockTusConfiguration))
            {
                var response = await server
                    .CreateRequest("/files")
                    .OverrideHttpMethodIfNeeded("OPTIONS", methodToUse)
                    .SendAsync(methodToUse);

                AssertContainsDefaultSuccessfulHeaders(response);
            }

            // Test again but with a store that does not implement any extensions.
            using (var server = TestServerFactory.Create(Substitute.For<ITusStore>()))
            {
                var response = await server.CreateRequest("/files").SendAsync("OPTIONS");

                response.ShouldContainHeader("Tus-Resumable", "1.0.0");
                response.ShouldContainHeader("Tus-Version", "1.0.0");
                response.Headers.Contains("Tus-Extension").ShouldBeFalse();
                response.Headers.Contains("Tus-Checksum-Algorithm").ShouldBeFalse();
            }
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Not_Included_If_No_Max_Size_Is_Configured()
        {
            using var server = TestServerFactory.Create(Substitute.For<ITusStore>());

            var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
            response.Headers.Contains("Tus-Max-Size").ShouldBeFalse();
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Included_If_Configured_Using_MaxAllowedUploadSizeInBytes()
        {
            _mockTusConfiguration.MaxAllowedUploadSizeInBytes = 100;

            using var server = TestServerFactory.Create(_mockTusConfiguration);

            var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
            response.ShouldContainHeader("Tus-Max-Size", "100");
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Included_If_Configured_Using_MaxAllowedUploadSizeInBytesLong()
        {
            const long maxSizeLong = (long)int.MaxValue + 1;
            _mockTusConfiguration.MaxAllowedUploadSizeInBytesLong = maxSizeLong;

            using var server = TestServerFactory.Create(_mockTusConfiguration);

            var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
            response.ShouldContainHeader("Tus-Max-Size", maxSizeLong.ToString());
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Included_If_Configured_Using_Both_MaxAllowedUploadSizeInBytes_And_MaxAllowedUploadSizeInBytesLong()
        {
            // MaxAllowedUploadSizeInBytesLong takes precedence.
            _mockTusConfiguration.MaxAllowedUploadSizeInBytes = 100;
            _mockTusConfiguration.MaxAllowedUploadSizeInBytesLong = 50;

            using var server = TestServerFactory.Create(_mockTusConfiguration);

            var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
            response.ShouldContainHeader("Tus-Max-Size", "50");
        }

        [Fact]
        public async Task OnAuthorized_Is_Called()
        {
            using var server = TestServerFactory.Create(_mockTusConfiguration);

            var response = await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS");

            AssertContainsDefaultSuccessfulHeaders(response);

            _onAuthorizeWasCalled.ShouldBeTrue();
            _onAuthorizeWasCalledWithIntent.ShouldBe(IntentType.GetOptions);
        }

        [Fact]
        public async Task Request_Is_Cancelled_If_OnAuthorized_Fails_The_Request()
        {
            using var server = TestServerFactory.Create(_mockTusConfiguration.Store, new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.FromResult(0);
                }
            });

            var response = await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.ShouldNotContainHeaders(
                "Tus-Resumable",
                "Tus-Version",
                "Tus-Extension",
                "Tus-Checksum-Algorithm",
                "Content-Type");
        }

        [Theory]
        [InlineData(typeof(ITusCreationStore), "creation,creation-with-upload")]
        [InlineData(typeof(ITusTerminationStore), "termination")]
#if trailingheaders
        [InlineData(typeof(ITusChecksumStore), "checksum,checksum-trailer")]
#else
        [InlineData(typeof(ITusChecksumStore), "checksum")]
#endif
        [InlineData(typeof(ITusConcatenationStore), "concatenation")]
        [InlineData(typeof(ITusExpirationStore), "expiration")]
        [InlineData(typeof(ITusCreationDeferLengthStore), "creation-defer-length")]
        public async Task Extensions_Depend_On_What_The_Store_Supports(Type storeInterfaceType, string expectedExtensions)
        {
            var store = (ITusStore)Substitute.For(new[] { typeof(ITusStore), storeInterfaceType }, null);

            using var server = TestServerFactory.Create(store);

            var response = await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS");

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.ShouldContainHeader("Tus-Extension", expectedExtensions);
        }

        private static void AssertContainsDefaultSuccessfulHeaders(System.Net.Http.HttpResponseMessage response)
        {
#if trailingheaders
            const bool pipelineSupportsChecksumTrailer = true;
#else
            const bool pipelineSupportsChecksumTrailer = false;
#endif

            const string expectedExtensions = pipelineSupportsChecksumTrailer
                ? "creation,creation-with-upload,termination,checksum,checksum-trailer,concatenation,expiration,creation-defer-length"
                : "creation,creation-with-upload,termination,checksum,concatenation,expiration,creation-defer-length";

            response.ShouldContainHeader("Tus-Resumable", "1.0.0");
            response.ShouldContainHeader("Tus-Version", "1.0.0");
            response.ShouldContainHeader("Tus-Extension", expectedExtensions);
            response.ShouldContainHeader("Tus-Checksum-Algorithm", "sha1");
        }

        private void AssertForwardCall(bool expectedValue)
        {
            _callForwarded.ShouldBe(expectedValue);
            _onAuthorizeWasCalled.ShouldBe(!expectedValue);

            _onAuthorizeWasCalled = false;
            _onAuthorizeWasCalledWithIntent = null;
            _callForwarded = false;
        }
    }
}