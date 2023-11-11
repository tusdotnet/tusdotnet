#if NETCOREAPP3_1_OR_GREATER

#pragma warning disable IDE0039 // Use local function - Requires additional casting.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;
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
        public void TusFileId_Is_Automatically_Included_In_Route_Pattern_As_Optional_Parameter(string pattern)
        {
            using var server = CreateTestServer(endpoints => endpoints.MapTus(pattern, _ => Task.FromResult(CreateConfig())));

            var expectedPatternWithTusFileId = pattern.TrimEnd('/') + "/{TusFileId?}";
            var endpoint = (RouteEndpoint)server.Services.GetService<EndpointDataSource>().Endpoints[0];
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
                using var server = CreateTestServer(endpoints => endpoints.MapTus(pattern, _ => Task.FromResult(CreateConfig())));
            });
        }

        [Fact]
        public async Task The_Endpoint_Configuration_Factory_Is_Used()
        {
            var endpointFactoryUsed = false;
            Func<HttpContext, Task<DefaultTusConfiguration>> endpointFactory = _ => Task.FromResult(CreateConfig(() => endpointFactoryUsed = true));

            using var server = await CreateTestServerAndSendOptionsRequest(app => app.MapTus("/", endpointFactory));
            endpointFactoryUsed.ShouldBeTrue();
        }

        [Fact]
        public async Task Ignores_Request_If_Url_Does_Not_Match()
        {
            using var server = CreateTestServer(endpoints => endpoints.MapTus("/files", _ => Task.FromResult(CreateConfig())));

            await SendAndAssert(server, "/files", HttpStatusCode.Created);

            await SendAndAssert(server, "/otherfiles", HttpStatusCode.NotFound);

            await SendAndAssert(server, "/files/testfile", HttpStatusCode.NotFound);

            static async Task SendAndAssert(TestServer server, string path, HttpStatusCode expectedStatusCode)
            {
                var response = await server.CreateRequest(path)
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Length", "100")
                    .SendAsync("POST");

                response.StatusCode.ShouldBe(expectedStatusCode);
            }
        }

        private static TestServer CreateTestServer(Action<IEndpointRouteBuilder> endpoints, Action<IServiceCollection> configureServices = null)
        {
            var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                configureServices?.Invoke(services);

            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints);
            });

            var server = new TestServer(builder);
            return server;
        }

        private static DefaultTusConfiguration CreateConfig(Action onAuthorizeCalled = null)
        {
            return new DefaultTusConfiguration
            {
                Store = Substitute.For<ITusStore, ITusCreationStore>(),
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

        private static async Task<TestServer> CreateTestServerAndSendOptionsRequest(Action<IEndpointRouteBuilder> endpoints)
        {
            TestServer server = CreateTestServer(endpoints);

            await SendOptionsRequest(server);

            return server;
        }
    }
}

#endif