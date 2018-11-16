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

            // ReSharper disable once PossibleNullReferenceException
            ((ITusChecksumStore)store).GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });

            _mockTusConfiguration = new DefaultTusConfiguration
            {
                Store = store,
                UrlPath = "/files"
            };
        }

        [Fact]
        public async Task Ignores_Request_If_Url_Does_Not_Match()
        {
            var callForwarded = false;
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(_ => _mockTusConfiguration);

                app.Use((_, __) =>
                {
                    callForwarded = true;
                    return Task.FromResult(true);
                });
            }))
            {
                await server
                        .CreateRequest("/files")
                        .AddTusResumableHeader()
                        .SendAsync("OPTIONS");

                callForwarded.ShouldBeFalse();

                await server
                    .CreateRequest("/otherfiles")
                    .AddTusResumableHeader()
                    .SendAsync("OPTIONS");

                callForwarded.ShouldBeTrue();

                callForwarded = false;

                await server
                        .CreateRequest("/files/testfile")
                        .AddTusResumableHeader()
                        .SendAsync("OPTIONS");

                callForwarded.ShouldBeTrue();
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Returns_204_NoContent_On_Success(string methodToUse)
        {
            using (var server = TestServerFactory.Create(app => app.UseTus(_ => _mockTusConfiguration)))
            {
                var response = await server
                    .CreateRequest("/files")
                    .OverrideHttpMethodIfNeeded("OPTIONS", methodToUse)
                    .SendAsync(methodToUse);
                response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }
        }

        [Theory, XHttpMethodOverrideData]
        public async Task Response_Contains_The_Correct_Headers_On_Success(string methodToUse)
        {
            using (var server = TestServerFactory.Create(app => app.UseTus(_ => _mockTusConfiguration)))
            {
                var response = await server
                    .CreateRequest("/files")
                    .OverrideHttpMethodIfNeeded("OPTIONS", methodToUse)
                    .SendAsync(methodToUse);

                response.ShouldContainHeader("Tus-Resumable", "1.0.0");
                response.ShouldContainHeader("Tus-Version", "1.0.0");
                response.ShouldContainHeader("Tus-Extension", "creation,termination,checksum,concatenation,expiration,creation-defer-length");
                response.ShouldContainHeader("Tus-Checksum-Algorithm", "sha1");
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
            using (var server = TestServerFactory.Create(Substitute.For<ITusStore>()))
            {
                var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
                response.Headers.Contains("Tus-Max-Size").ShouldBeFalse();
            }
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Included_If_Configured_Using_MaxAllowedUploadSizeInBytes()
        {
            _mockTusConfiguration.MaxAllowedUploadSizeInBytes = 100;

            using (var server = TestServerFactory.Create(app => app.UseTus(_ => _mockTusConfiguration)))
            {
                var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
                response.ShouldContainHeader("Tus-Max-Size", "100");
            }
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Included_If_Configured_Using_MaxAllowedUploadSizeInBytesLong()
        {
            const long maxSizeLong = (long)int.MaxValue + 1;
            _mockTusConfiguration.MaxAllowedUploadSizeInBytesLong = maxSizeLong;

            using (var server = TestServerFactory.Create(app => app.UseTus(_ => _mockTusConfiguration)))
            {
                var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
                response.ShouldContainHeader("Tus-Max-Size", maxSizeLong.ToString());
            }
        }

        [Fact]
        public async Task Tus_Max_Size_Is_Included_If_Configured_Using_Both_MaxAllowedUploadSizeInBytes_And_MaxAllowedUploadSizeInBytesLong()
        {
            // MaxAllowedUploadSizeInBytesLong takes precedence.
            _mockTusConfiguration.MaxAllowedUploadSizeInBytes = 100;
            _mockTusConfiguration.MaxAllowedUploadSizeInBytesLong = 50;

            using (var server = TestServerFactory.Create(app => app.UseTus(_ => _mockTusConfiguration)))
            {
                var response = await server.CreateRequest("/files").SendAsync("OPTIONS");
                response.ShouldContainHeader("Tus-Max-Size", "50");
            }
        }
    }
}