using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Testing;
using NSubstitute;
using Owin;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Stores;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
	public class DeleteTests
	{
		[Theory, XHttpMethodOverrideData]
		public async Task Ignores_Request_If_Url_Does_Not_Match(string methodToUse)
		{
			var callForwarded = false;
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = new TusDiskStore(@"C:\temp"),
					UrlPath = "/files"
				});

				app.Use((ctx, next) =>
				{
					callForwarded = true;
					return Task.FromResult(true);
				});

			}))
			{
				await server
					.CreateRequest("/files")
					.AddHeader("Tus-Resumable", "1.0.0")
					.OverrideHttpMethodIfNeeded("DELETE", methodToUse)
					.SendAsync(methodToUse);

				callForwarded.ShouldBeTrue();

				callForwarded = false;

				await server
					.CreateRequest("/files/testfiledelete")
					.AddHeader("Tus-Resumable", "1.0.0")
					.OverrideHttpMethodIfNeeded("DELETE", methodToUse)
					.SendAsync(methodToUse);

				callForwarded.ShouldBeFalse();

				await server
					.CreateRequest("/otherfiles/testfiledelete")
					.AddHeader("Tus-Resumable", "1.0.0")
					.OverrideHttpMethodIfNeeded("DELETE", methodToUse)
					.SendAsync(methodToUse);

				callForwarded.ShouldBeTrue();
			}

		}

		[Theory, XHttpMethodOverrideData]
		public async Task Returns_204_No_Content_On_Success(string methodToUse)
		{
			using (var server = TestServer.Create(app =>
			{
				var store = Substitute.For<ITusStore, ITusTerminationStore>();
				store.FileExistAsync("testfiledelete", Arg.Any<CancellationToken>()).Returns(true);

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});
			}))
			{

				var response = await server
					.CreateRequest("/files/testfiledelete")
					.AddTusResumableHeader()
					.OverrideHttpMethodIfNeeded("DELETE", methodToUse)
					.SendAsync(methodToUse);

				response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
				response.ShouldContainTusResumableHeader();
			}
		}

		[Theory, XHttpMethodOverrideData]
		public async Task Returns_409_Conflict_If_Multiple_Requests_Try_To_Delete_The_Same_File(string methodToUse)
		{
			using (var server = TestServer.Create(app =>
			{
				var random = new Random();
				var store = Substitute.For<ITusStore, ITusTerminationStore>();
				var terminationStore = (ITusTerminationStore)store;
				store.FileExistAsync("testfiledelete", Arg.Any<CancellationToken>()).Returns(true);
				terminationStore
					.DeleteFileAsync("testfiledelete", Arg.Any<CancellationToken>())
					.Returns(info =>
					{
						// Emulate some latency in the request.
						Thread.Sleep(random.Next(100, 301));
						return Task.FromResult(0);
					});

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});

			}))
			{
				var task1 = server
					.CreateRequest("/files/testfiledelete")
					.AddTusResumableHeader()
					.OverrideHttpMethodIfNeeded("DELETE", methodToUse)
					.SendAsync(methodToUse);
				var task2 = server
					.CreateRequest("/files/testfiledelete")
					.AddTusResumableHeader()
					.OverrideHttpMethodIfNeeded("DELETE", methodToUse)
					.SendAsync(methodToUse);

				await Task.WhenAll(task1, task2);

				if (task1.Result.StatusCode == HttpStatusCode.NoContent)
				{
					task1.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
					task2.Result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
				}
				else
				{
					task1.Result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
					task2.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
				}
			}
		}
	}
}