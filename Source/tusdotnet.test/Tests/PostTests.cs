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
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
	public class PostTests
	{
		[Fact]
		public async Task Ignores_Request_If_Url_Does_Not_Match()
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
					.SendAsync("POST");

				callForwarded.ShouldBeFalse();

				await server
					.CreateRequest("/otherfiles")
					.AddHeader("Tus-Resumable", "1.0.0")
					.SendAsync("POST");

				callForwarded.ShouldBeTrue();

				callForwarded = false;

				await server
					.CreateRequest("/files/testfile")
					.AddHeader("Tus-Resumable", "1.0.0")
					.SendAsync("POST");

				callForwarded.ShouldBeTrue();
			}
		}

		[Fact]
		public async Task Forwards_Calls_If_The_Store_Does_Not_Support_Creation()
		{
			var callForwared = false;
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = Substitute.For<ITusStore>(),
					UrlPath = "/files"
				});

				app.Use((context, func) =>
				{
					callForwared = true;
					return Task.FromResult(true);
				});
			}))
			{
				await server.CreateRequest("/files").PostAsync();
				callForwared.ShouldBeTrue();
			}
		}

		[Fact]
		public async Task Returns_400_Bad_Request_If_Upload_Length_Is_Not_Specified()
		{
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = Substitute.For<ITusStore, ITusCreationStore>(),
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.PostAsync();
				await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest, "Missing Upload-Length header");
			}
		}

		[Theory]
		[InlineData("uploadlength", "")]
		[InlineData("0.1", "")]
		[InlineData("-100", "Header Upload-Length must be a positive number")]
		public async Task Returns_400_Bad_Request_If_Upload_Length_Is_Invalid(string uploadLength,
			string expectedServerErrorMessage)
		{
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = Substitute.For<ITusStore, ITusCreationStore>(),
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", uploadLength)
					.PostAsync();
				await
					response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
						string.IsNullOrEmpty(expectedServerErrorMessage) ? "Could not parse Upload-Length" : expectedServerErrorMessage);
			}
		}

		[Fact]
		public async Task Returns_201_Created_On_Success()
		{
			using (var server = TestServer.Create(app =>
			{

				var tusStore = Substitute.For<ITusCreationStore, ITusStore>();
				tusStore.CreateFileAsync(1, CancellationToken.None).ReturnsForAnyArgs("fileId");

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = tusStore as ITusStore,
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.Created);
			}
		}

		[Fact]
		public async Task Response_Contains_The_Correct_Headers_On_Success()
		{
			using (var server = TestServer.Create(app =>
			{

				var tusStore = Substitute.For<ITusCreationStore, ITusStore>();
				tusStore.CreateFileAsync(1, CancellationToken.None).ReturnsForAnyArgs("fileId");

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = tusStore as ITusStore,
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.PostAsync();

				response.ShouldContainTusResumableHeader();
				response.Headers.Location.ToString().ShouldBe("/files/fileId");
			}
		}
	}
}
