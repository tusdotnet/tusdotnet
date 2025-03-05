#if NETCOREAPP3_1_OR_GREATER

#pragma warning disable IDE0039 // Use local function - Requires additional casting.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using tusdotnet.Constants;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class EndpointRoutingTests
    {
        [Theory]
        [InlineData("/files")]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("/files/a/b/c")]
        public void TusFileId_Is_Automatically_Included_In_Route_Pattern_As_Optional_Parameter(
            string pattern
        )
        {
            using var server = CreateTestServer(endpoints =>
                endpoints.MapTus(pattern, _ => Task.FromResult(CreateConfig()))
            );

            var expectedPatternWithTusFileId = pattern.TrimEnd('/') + "/{TusFileId?}";
            var endpoint = (RouteEndpoint)
                server.Services.GetService<EndpointDataSource>().Endpoints[0];
            endpoint.RoutePattern.RawText.ShouldBe(expectedPatternWithTusFileId);
            endpoint.DisplayName.ShouldBe("tus: " + expectedPatternWithTusFileId);
        }

        [Theory]
        [InlineData("/files/{TusFileId}")]
        [InlineData("{TusFileId}")]
        [InlineData("/{TusFileId}")]
        [InlineData("/files/{TusFileId}/a/b/c")]
        [InlineData("/files/a/b/{TusFileId}/c")]
        public void Throws_Exception_If_TusFileId_Is_Included_In_Route_Pattern(string pattern)
        {
            Should.Throw<ArgumentException>(() =>
            {
                using var server = CreateTestServer(endpoints =>
                    endpoints.MapTus(pattern, _ => Task.FromResult(CreateConfig()))
                );
            });
        }

        [Fact]
        public async Task The_Endpoint_Configuration_Factory_Is_Used()
        {
            var endpointFactoryUsed = false;
            Func<HttpContext, Task<DefaultTusConfiguration>> endpointFactory = _ =>
                Task.FromResult(CreateConfig(() => endpointFactoryUsed = true));

            using var server = await CreateTestServerAndSendOptionsRequest(app =>
                app.MapTus("/", endpointFactory)
            );
            endpointFactoryUsed.ShouldBeTrue();
        }

        [Theory]
        [InlineData("/files", HttpStatusCode.Created)]
        [InlineData("/otherfiles", HttpStatusCode.NotFound)]
        [InlineData("/files/testfile", HttpStatusCode.NotFound)]
        public async Task Ignores_Request_If_Url_Does_Not_Match(
            string path,
            HttpStatusCode expectedStatusCode
        )
        {
            // Pretend that all files exist so that we do not send 404 for that reason.
            var config = CreateConfig();
            config.Store.FileExistAsync(default, default).ReturnsForAnyArgs(true);

            using var server = CreateTestServer(endpoints =>
                endpoints.MapTus("/files", _ => Task.FromResult(config))
            );

            var response = await server
                .CreateRequest(path)
                .AddTusResumableHeader()
                .AddHeader("Upload-Length", "100")
                .SendAsync("POST");

            response.StatusCode.ShouldBe(expectedStatusCode);
        }

        [Theory]
        [InlineData("/mybase/files")]
        [InlineData("/files")]
        [InlineData("/mybase/files", "final;/mybase/files/file1 /mybase/files/file1")]
        [InlineData("/files", "final;/files/file1 /files/file1")]
        public async Task Includes_PathBase_In_Location_When_Creating_A_File_If_UsePathBase_Is_Set(
            string path,
            string uploadConcatHeader = null
        )
        {
            // Pretend that all files exist so that we do not send 404 for that reason.
            var config = CreateConfig();
            config.Store.FileExistAsync(default, default).ReturnsForAnyArgs(true);
            config.Store.WithExistingFile("file1", 100, 100);

            ((ITusConcatenationStore)config.Store)
                .GetUploadConcatAsync(default, default)
                .ReturnsForAnyArgs(new FileConcatPartial());

            using var server = CreateTestServer(null, ConfigureServer);

            var request = server
                .CreateRequest(path)
                .AddTusResumableHeader()
                .AddHeader("Upload-Length", "100");

            if (!string.IsNullOrEmpty(uploadConcatHeader))
            {
                request = request.AddHeader("Upload-Concat", uploadConcatHeader);
            }

            var response = await request.SendAsync("POST");

            response.StatusCode.ShouldBe(
                HttpStatusCode.Created,
                await response.Content.ReadAsStringAsync()
            );

            response
                .Headers.TryGetValues(HeaderConstants.Location, out var location)
                .ShouldBeTrue();
            location.First().ShouldStartWith(path);

            void ConfigureServer(IApplicationBuilder app)
            {
                app.UsePathBase("/mybase");
                app.UseRouting();
                app.UseEndpoints(e => e.MapTus("/files", _ => Task.FromResult(config)));
            }
        }

        [Theory]
        [InlineData("/mybase/files")]
        [InlineData("/files")]
        public async Task Includes_PathBase_In_Upload_Concat_Header_When_Getting_Final_File_Info_If_UsePathBase_Is_Set(
            string path
        )
        {
            // Pretend that all files exist so that we do not send 404 for that reason.
            var config = CreateConfig();
            config.Store.FileExistAsync(default, default).ReturnsForAnyArgs(true);

            var concatStore = (ITusConcatenationStore)config.Store;
            concatStore
                .GetUploadConcatAsync(default, default)
                .ReturnsForAnyArgs(new FileConcatFinal("partial1", "partial2"));

            using var server = CreateTestServer(null, ConfigureServer);

            var response = await server
                .CreateRequest(path + "/finalfile")
                .AddTusResumableHeader()
                .SendAsync("HEAD");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response
                .Headers.TryGetValues(HeaderConstants.UploadConcat, out var uploadConcatHeader)
                .ShouldBeTrue();

            var uploadConcat = new UploadConcat(uploadConcatHeader.First());
            uploadConcat.IsValid.ShouldBeTrue();
            uploadConcat.ErrorMessage.ShouldBeNull();

            var fileConcatFinal = uploadConcat.Type as FileConcatFinal;
            fileConcatFinal.ShouldNotBeNull();
            fileConcatFinal.Files[0].ShouldBe(path.TrimStart('/') + "/" + "partial1");
            fileConcatFinal.Files[1].ShouldBe(path.TrimStart('/') + "/" + "partial2");

            void ConfigureServer(IApplicationBuilder app)
            {
                app.UsePathBase("/mybase");
                app.UseRouting();
                app.UseEndpoints(e => e.MapTus("/files", _ => Task.FromResult(config)));
            }
        }

        private static TestServer CreateTestServer(
            Action<IEndpointRouteBuilder> endpoints,
            Action<IApplicationBuilder> configure = null
        )
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    if (configure is not null)
                    {
                        configure.Invoke(app);
                    }
                    else
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints);
                    }
                });

            var server = new TestServer(builder);
            return server;
        }

        private static DefaultTusConfiguration CreateConfig(Action onAuthorizeCalled = null)
        {
            return new DefaultTusConfiguration
            {
                Store = Substitute.For<ITusStore, ITusCreationStore, ITusConcatenationStore>(),
                Events = new()
                {
                    OnAuthorizeAsync = _ =>
                    {
                        onAuthorizeCalled?.Invoke();
                        return Task.CompletedTask;
                    }
                }
            };
        }

        private static Task<HttpResponseMessage> SendOptionsRequest(TestServer server)
        {
            var client = server.CreateClient();
            return client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/"));
        }

        private static async Task<TestServer> CreateTestServerAndSendOptionsRequest(
            Action<IEndpointRouteBuilder> endpoints
        )
        {
            TestServer server = CreateTestServer(endpoints);

            await SendOptionsRequest(server);

            return server;
        }
    }
}

#endif
