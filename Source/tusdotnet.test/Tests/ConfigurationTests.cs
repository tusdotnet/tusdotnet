using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Extensions;
using Xunit;
#if netfull
using Owin;
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
            var tusConfiguration = Substitute.For<ITusConfiguration>();
            tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
            tusConfiguration.UrlPath.Returns("/files");

#if netfull
            var configFunc = Substitute.For<Func<IOwinRequest, ITusConfiguration>>();
            configFunc.Invoke(Arg.Any<IOwinRequest>()).Returns(tusConfiguration);
#else
			var configFunc = Substitute.For<Func<HttpContext, ITusConfiguration>>();
			configFunc.Invoke(Arg.Any<HttpContext>()).Returns(tusConfiguration);
#endif

            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(configFunc);
            }))
            {
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
        }

        [Fact]
        public async Task Configuration_Is_Validated_On_Each_Request()
        {
            var tusConfiguration = Substitute.For<ITusConfiguration>();

            // Empty configuration
            using (var server = TestServerFactory.Create(app =>
            {
                // ReSharper disable once AccessToModifiedClosure
                app.UseTus(request => tusConfiguration);
            }))
            {
                // ReSharper disable once AccessToDisposedClosure
                await AssertRequests(server);
            }

            // Configuration with only Store specified
            tusConfiguration = Substitute.For<ITusConfiguration>();
            tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
            using (var server = TestServerFactory.Create(app =>
            {
                // ReSharper disable once AccessToModifiedClosure
                app.UseTus(request => tusConfiguration);
            }))
            {
                // ReSharper disable once AccessToDisposedClosure
                await AssertRequests(server);
            }

            tusConfiguration = Substitute.For<ITusConfiguration>();
            tusConfiguration.UrlPath.Returns("/files");
            tusConfiguration.Store.Returns((ITusStore)null);
            using (var server = TestServerFactory.Create(app =>
            {
                app.UseTus(request => tusConfiguration);
            }))
            {
                // ReSharper disable once AccessToDisposedClosure
                await AssertRequests(server);
            }
        }

        private static async Task AssertRequests(TestServer server)
        {

            var funcs = new List<Func<Task>>()
            {
                async () => await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("OPTIONS"),
                async () => await server.CreateRequest("/files").AddTusResumableHeader().SendAsync("POST"),
                async () => await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("HEAD"),
                async () => await server.CreateRequest("/files/testfile").AddTusResumableHeader().SendAsync("PATCH")
            };

            foreach (var func in funcs)
            {
                await Should.ThrowAsync<TusConfigurationException>(async () => await func());
            }

        }

    }
}
