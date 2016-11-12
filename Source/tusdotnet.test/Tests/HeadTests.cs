using System.Linq;
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
	public class HeadTests
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
					.SendAsync("HEAD");

				callForwarded.ShouldBeTrue();

				callForwarded = false;

				await server
					.CreateRequest("/files/testfile")
					.AddHeader("Tus-Resumable", "1.0.0")
					.SendAsync("HEAD");

				callForwarded.ShouldBeFalse();

				await server
					.CreateRequest("/otherfiles/testfile")
					.AddHeader("Tus-Resumable", "1.0.0")
					.SendAsync("HEAD");

				callForwarded.ShouldBeTrue();
			}
		}

		[Fact]
		public async Task Returns_404_Not_Found_If_File_Was_Not_Found()
		{
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = Substitute.For<ITusStore>(),
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files/testfile")
					.AddTusResumableHeader()
					.SendAsync("HEAD");

				response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
			}
		}

		[Fact]
		public async Task Includes_Upload_Length_Header_If_Available()
		{
			using (var server = TestServer.Create(app =>
			{
				var store = Substitute.For<ITusStore>();
				store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
				store.FileExistAsync("otherfile", Arg.Any<CancellationToken>()).Returns(true);
				store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(100);
				store.GetUploadLengthAsync("otherfile", Arg.Any<CancellationToken>()).Returns(null as long?);

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files/testfile")
					.AddTusResumableHeader()
					.SendAsync("HEAD");

				response.Headers.Contains("Upload-Length").ShouldBeTrue();
				var uploadLength = response.Headers.GetValues("Upload-Length").ToList();
				uploadLength.Count.ShouldBe(1);
				uploadLength.First().ShouldBe("100");

				response = await server
					.CreateRequest("/files/otherfile")
					.AddTusResumableHeader()
					.SendAsync("HEAD");
				response.Headers.Contains("Upload-Length").ShouldBeFalse();
			}
		}

		[Fact]
		public async Task Response_Contains_The_Correct_Headers_On_Success()
		{
			using (var server = TestServer.Create(app =>
			{
				var store = Substitute.For<ITusStore>();
				store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
				store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(100);
				store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(50);

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files/testfile")
					.AddTusResumableHeader()
					.SendAsync("HEAD");

				response.ShouldContainTusResumableHeader();
				response.ShouldContainHeader("Upload-Length", "100");
				response.ShouldContainHeader("Upload-Offset", "50");
				response.ShouldContainHeader("Cache-Control", "no-store");
			}
		}

		//[Fact]
		//public async Task Response_Contains_Metadata_If_It_Exists_For_File()
		//{
		//	// If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata header 
		//	// and its value as specified by the Client during the creation.

		//	throw new NotImplementedException();
		//}
	}
}
