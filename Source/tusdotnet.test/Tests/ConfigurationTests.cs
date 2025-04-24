using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Extensions;
using Xunit;
#if netfull
using Microsoft.Owin.Testing;
using Microsoft.Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
#endif

namespace tusdotnet.test.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public async Task Creates_A_Configuration_Instance_Per_Request()
        {
            var tusConfiguration = new DefaultTusConfiguration
            {
                Store = Substitute.For<ITusStore>(),
                UrlPath = "/files",
            };

#if netfull
            var configFunc = Substitute.For<Func<IOwinRequest, Task<DefaultTusConfiguration>>>();
            configFunc.Invoke(Arg.Any<IOwinRequest>()).Returns(tusConfiguration);
#else

#if NET6_0_OR_GREATER
            tusConfiguration.UrlPath = null;
#endif
            var configFunc = Substitute.For<Func<HttpContext, Task<DefaultTusConfiguration>>>();
            configFunc.Invoke(Arg.Any<HttpContext>()).Returns(tusConfiguration);
#endif

            using var server = TestServerFactory.CreateWithFactory(configFunc, "/files");

            // Test OPTIONS
            for (var i = 0; i < 3; i++)
            {
                await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS");
            }

            // Test POST
            for (var i = 0; i < 3; i++)
            {
                await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("POST");
            }

            // Test HEAD
            for (var i = 0; i < 3; i++)
            {
                await server
                    .CreateRequest("/files/testfile")
                    .AddTusResumableHeader()
                    .SendAsync("HEAD");
            }

            // Test PATCH
            for (var i = 0; i < 3; i++)
            {
                await server
                    .CreateRequest("/files/testfile")
                    .AddTusResumableHeader()
                    .SendAsync("PATCH");
            }

            configFunc.ReceivedCalls().Count().ShouldBe(12);
        }

        [Fact]
        public async Task Configuration_Is_Validated_On_Each_Request()
        {
            var tusConfiguration = new DefaultTusConfiguration();

            // Empty configuration
            using (var server = TestServerFactory.Create(tusConfiguration))
            {
                await AssertRequests(server);
            }

            // Configuration with only Store specified
            tusConfiguration = new DefaultTusConfiguration { Store = Substitute.For<ITusStore>() };
            using (var server = TestServerFactory.Create(tusConfiguration))
            {
                await AssertRequests(server);
            }

            // Configuration with only url path specified
            tusConfiguration = new DefaultTusConfiguration { UrlPath = "/files", Store = null };
            using (var server = TestServerFactory.Create(tusConfiguration))
            {
                await AssertRequests(server);
            }

            static async Task AssertRequests(TestServer server)
            {
                var funcs = new List<Func<Task>>(4)
                {
                    () =>
                        server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS"),
                    () => server.CreateRequest("/files").AddTusResumableHeader().SendAsync("POST"),
                    () =>
                        server
                            .CreateRequest("/files/testfile")
                            .AddTusResumableHeader()
                            .SendAsync("HEAD"),
                    () =>
                        server
                            .CreateRequest("/files/testfile")
                            .AddTusResumableHeader()
                            .SendAsync("PATCH"),
                };

                foreach (var func in funcs)
                {
                    try
                    {
                        await func();
                    }
                    catch (TusConfigurationException)
                    {
                        // This is correct, so just ignore it.
                    }
                }
            }
        }

        [Fact]
        public async Task Supports_Async_Configuration_Factories()
        {
            var urlPath = $"/{Guid.NewGuid()}";
            var tusConfiguration = new DefaultTusConfiguration
            {
                UrlPath = urlPath,
                Store = Substitute.For<ITusStore>(),
            };

#if NET6_0_OR_GREATER
            // Not supported for endpoint routing.
            tusConfiguration.UrlPath = null;
#endif

            // Empty configuration
            using var server = TestServerFactory.CreateWithFactory(
                async httpContext =>
                {
                    await Task.Delay(10);
                    return tusConfiguration;
                },
                urlPath
            );

            var response = await server.CreateRequest(urlPath).SendAsync("OPTIONS");
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.ShouldContainHeader("Tus-Resumable", "1.0.0");
        }

        [Fact]
        public async Task File_Lock_Provider_Is_Called_If_A_Lock_Is_Required()
        {
            var lockProvider = new FileLockProviderForConfigurationTests();
            var tusConfiguration = new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = Substitute.For<ITusStore, ITusTerminationStore, ITusCreationStore>(),
                FileLockProvider = lockProvider,
            };

            using var server = TestServerFactory.Create(tusConfiguration);

            const string urlPath = "/files/";
            var fileIdUrl = urlPath + Guid.NewGuid();

            // PATCH, DELETE and HEAD are using locks.
            var response = await server.CreateTusResumableRequest(fileIdUrl).SendAsync("PATCH");
            response = await server.CreateTusResumableRequest(fileIdUrl).SendAsync("DELETE");
            response = await server.CreateTusResumableRequest(fileIdUrl).SendAsync("HEAD");

            // Others will not lock.
            response = await server.CreateTusResumableRequest(urlPath).SendAsync("OPTIONS");
            response = await server.CreateTusResumableRequest(urlPath).SendAsync("POST");

            lockProvider.LockCount.ShouldBe(3);
            lockProvider.ReleaseCount.ShouldBe(3);
        }

        private class FileLockProviderForConfigurationTests : ITusFileLockProvider
        {
            public int LockCount { get; set; }

            public int ReleaseCount { get; set; }

            public Task<ITusFileLock> AquireLock(string fileId)
            {
                return Task.FromResult<ITusFileLock>(new FileLockForConfigurationTests(this));
            }
        }

        private class FileLockForConfigurationTests : ITusFileLock
        {
            private readonly FileLockProviderForConfigurationTests _provider;

            public FileLockForConfigurationTests(FileLockProviderForConfigurationTests provider)
            {
                _provider = provider;
            }

            public Task<bool> Lock()
            {
                _provider.LockCount++;
                return Task.FromResult(true);
            }

            public Task ReleaseIfHeld()
            {
                _provider.ReleaseCount++;
                return TaskHelper.Completed;
            }
        }
    }
}
