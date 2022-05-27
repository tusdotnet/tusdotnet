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
using System.Net.Http;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;
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
            using var server = CreateTestServer(endpoints => endpoints.MapTus(pattern));

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
                using var server = CreateTestServer(endpoints => endpoints.MapTus(pattern));
            });
        }

        [Fact]
        public async Task The_Endpoint_Configuration_Factory_Is_Used_If_Specified()
        {
            var iocFactoryUsed = false;

            Func<HttpContext, Task<DefaultTusConfiguration>> iocFactory = _ => Task.FromResult(CreateConfig(() => iocFactoryUsed = true));

            var iocConfigUsed = false;
            DefaultTusConfiguration iocConfig = CreateConfig(() => iocConfigUsed = true);

            var endpointFactoryUsed = false;
            Func<HttpContext, Task<DefaultTusConfiguration>> endpointFactory = _ => Task.FromResult(CreateConfig(() => endpointFactoryUsed = true));

            using var server = await CreateTestServerAndSendOptionsRequest(app => app.MapTus("/", endpointFactory), configureServices);
            FirstShouldBeTrue(endpointFactoryUsed, iocFactoryUsed, iocConfigUsed);

            // Register factory + config in ioc
            void configureServices(IServiceCollection services)
            {
                services.AddSingleton(_ => iocFactory);
                services.AddSingleton(_ => iocConfig);
            }
        }

        [Fact]
        public async Task The_Endpoint_Configuration_Is_Used_If_Specified()
        {
            var iocFactoryUsed = false;
            Func<HttpContext, Task<DefaultTusConfiguration>> iocFactory = _ => Task.FromResult(CreateConfig(() => iocFactoryUsed = true));

            var iocConfigUsed = false;
            DefaultTusConfiguration iocConfig = CreateConfig(() => iocConfigUsed = true);

            var endpointConfigUsed = false;
            var endpointConfig = CreateConfig(() => endpointConfigUsed = true);

            using var server = await CreateTestServerAndSendOptionsRequest(app => app.MapTus("/", endpointConfig), configureServices);
            FirstShouldBeTrue(endpointConfigUsed, iocFactoryUsed, iocConfigUsed);

            // Register factory + config in ioc
            void configureServices(IServiceCollection services)
            {
                services.AddSingleton(_ => iocFactory);
                services.AddSingleton(_ => iocConfig);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task The_Configuration_Is_Resolved_From_IOC_Container_If_No_Endpoint_Specific_Config_Have_Been_Specified(bool registerIocFactory)
        {
            var iocFactoryUsed = false;
            Func<HttpContext, Task<DefaultTusConfiguration>> iocFactory = _ => Task.FromResult(CreateConfig(() => iocFactoryUsed = true));

            var iocConfigUsed = false;
            DefaultTusConfiguration iocConfig = CreateConfig(() => iocConfigUsed = true);

            using var server = await CreateTestServerAndSendOptionsRequest(app => app.MapTus("/"), configureServices);

            if (registerIocFactory)
            {
                FirstShouldBeTrue(iocFactoryUsed, iocConfigUsed);
            }
            else
            {
                FirstShouldBeTrue(iocConfigUsed, iocFactoryUsed);
            }

            void configureServices(IServiceCollection services)
            {
                if (registerIocFactory)
                {
                    services.AddSingleton(_ => iocFactory);
                }
                else
                {
                    services.AddSingleton(_ => iocConfig);
                }
            }
        }

        [Fact]
        public async Task A_TusConfigurationException_Is_Thrown_If_No_Config_Was_Found_In_The_IOC_Container()
        {
            using var server = CreateTestServer(app => app.MapTus("/"), configureServices);

            var exception = await Should.ThrowAsync<TusConfigurationException>(async () => await SendOptionsRequest(server));
            exception.Message.ShouldBe("No configuration found. Searched the configuration factory provided when running MapTus and IoC container for Func<HttpContext, Task<DefaultTusConfiguration>> and DefaultTusConfiguration.");

            static void configureServices(IServiceCollection _) { }
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

        private static DefaultTusConfiguration CreateConfig(Action onAuthorizeCalled)
        {
            return new DefaultTusConfiguration
            {
                Store = Substitute.For<ITusStore>(),
                Events = new()
                {
                    OnAuthorizeAsync = _ =>
                    {
                        onAuthorizeCalled();
                        return Task.CompletedTask;
                    }
                }
            };
        }

        private static void FirstShouldBeTrue(bool expectedTrue, params bool[] expectedFalse)
        {
            expectedTrue.ShouldBeTrue();
            expectedFalse.ShouldAllBe(b => b == false);
        }

        private static Task<HttpResponseMessage> SendOptionsRequest(TestServer server)
        {
            var client = server.CreateClient();
            return client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/"));
        }

        private static async Task<TestServer> CreateTestServerAndSendOptionsRequest(Action<IEndpointRouteBuilder> endpoints, Action<IServiceCollection> configureServices)
        {
            TestServer server = CreateTestServer(endpoints, configureServices);

            await SendOptionsRequest(server);

            return server;
        }
    }
}

#endif