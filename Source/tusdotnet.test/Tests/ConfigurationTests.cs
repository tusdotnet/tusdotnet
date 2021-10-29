using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Extensions;
using Xunit;
using tusdotnet.Helpers;
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
            var tusConfiguration = Substitute.For<DefaultTusConfiguration>();
            tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
            tusConfiguration.UrlPath.Returns("/files");

#if netfull
            var configFunc = Substitute.For<Func<IOwinRequest, DefaultTusConfiguration>>();
            configFunc.Invoke(Arg.Any<IOwinRequest>()).Returns(tusConfiguration);
#else
            var configFunc = Substitute.For<Func<HttpContext, DefaultTusConfiguration>>();
            configFunc.Invoke(Arg.Any<HttpContext>()).Returns(tusConfiguration);
#endif

            using var server = TestServerFactory.Create(app => app.UseTus(configFunc));

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
                await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("HEAD");
            }

            // Test PATCH
            for (var i = 0; i < 3; i++)
            {
                await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("PATCH");
            }

            configFunc.ReceivedCalls().Count().ShouldBe(12);
        }

        [Fact]
        public async Task Configuration_Is_Validated_On_Each_Request()
        {
            var tusConfiguration = Substitute.For<DefaultTusConfiguration>();

            // Empty configuration
            using (var server = TestServerFactory.Create(tusConfiguration))
            {
                await AssertRequests(server);
            }

            // Configuration with only Store specified
            tusConfiguration = Substitute.For<DefaultTusConfiguration>();
            tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
            using (var server = TestServerFactory.Create(tusConfiguration))
            {
                await AssertRequests(server);
            }

            // Configuration with only url path specified
            tusConfiguration = Substitute.For<DefaultTusConfiguration>();
            tusConfiguration.UrlPath.Returns("/files");
            tusConfiguration.Store.Returns((ITusStore)null);
            using (var server = TestServerFactory.Create(tusConfiguration))
            {
                await AssertRequests(server);
            }

            static async Task AssertRequests(TestServer server)
            {
                var funcs = new List<Func<Task>>(4)
                {
                    () => server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS"),
                    () => server.CreateRequest("/files").AddTusResumableHeader().SendAsync("POST"),
                    () => server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("HEAD"),
                    () => server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("PATCH")
                };

                foreach (var func in funcs)
                {
                    await Should.ThrowAsync<TusConfigurationException>(async () => await func());
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
                Store = Substitute.For<ITusStore>()
            };

            // Empty configuration
            using var server = TestServerFactory.Create(app =>
            {
                app.UseTus(async _ =>
                {
                    await Task.Delay(10);
                    return tusConfiguration;
                });
            });

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
                FileLockProvider = lockProvider
            };

            using var server = TestServerFactory.Create(tusConfiguration);

            const string urlPath = "/files/";
            var fileIdUrl = urlPath + Guid.NewGuid();

            // PATCH and DELETE are using locks.
            var response = await server.CreateTusResumableRequest(fileIdUrl).SendAsync("PATCH");
            response = await server.CreateTusResumableRequest(fileIdUrl).SendAsync("DELETE");

            // Others will not lock.
            response = await server.CreateTusResumableRequest(urlPath).SendAsync("OPTIONS");
            response = await server.CreateTusResumableRequest(urlPath).SendAsync("POST");
            response = await server.CreateTusResumableRequest(fileIdUrl).SendAsync("HEAD");

            lockProvider.LockCount.ShouldBe(2);
            lockProvider.ReleaseCount.ShouldBe(2);
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