using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Owin.Testing;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Extensions;
using Xunit;

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

			var configFunc = Substitute.For<Func<ITusConfiguration>>();
			configFunc.Invoke().Returns(tusConfiguration);

			using (var server = TestServer.Create(app =>
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
			using (var server = TestServer.Create(app =>
			{
				// ReSharper disable once AccessToModifiedClosure
				app.UseTus(() => tusConfiguration);
			}))
			{
				// ReSharper disable once AccessToDisposedClosure
				await AssertRequests(server);
			}

			// Configuration with only Store specified
			tusConfiguration = Substitute.For<ITusConfiguration>();
			tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
			using (var server = TestServer.Create(app =>
			{
				// ReSharper disable once AccessToModifiedClosure
				app.UseTus(() => tusConfiguration);
			}))
			{
				// ReSharper disable once AccessToDisposedClosure
				await AssertRequests(server);
			}

			tusConfiguration = Substitute.For<ITusConfiguration>();
			tusConfiguration.UrlPath.Returns("/files");
			tusConfiguration.Store.Returns(null as ITusStore);
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(() => tusConfiguration);
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
