using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Stores;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;
#if netfull
using Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.Builder;
#endif

namespace tusdotnet.test.Tests
{
	public class HeadTests
	{
		[Fact]
		public async Task Ignores_Request_If_Url_Does_Not_Match()
		{
			var callForwarded = false;
			using (var server = TestServerFactory.Create(app =>
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
			using (var server = TestServerFactory.Create(app =>
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
				response.ShouldContainTusResumableHeader();
			}
		}

		[Fact]
		public async Task Includes_Upload_Length_Header_If_Available()
		{
			using (var server = TestServerFactory.Create(app =>
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

		[Theory, XHttpMethodOverrideData]
		public async Task Response_Contains_The_Correct_Headers_On_Success(string methodToUse)
		{
			using (var server = TestServerFactory.Create(app =>
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
					.OverrideHttpMethodIfNeeded("HEAD", methodToUse)
					.SendAsync(methodToUse);

				response.ShouldContainTusResumableHeader();
				response.ShouldContainHeader("Upload-Length", "100");
				response.ShouldContainHeader("Upload-Offset", "50");
				response.ShouldContainHeader("Cache-Control", "no-store");
			}
		}

		[Fact]
		public async Task Response_Contains_UploadMetadata_If_Metadata_Exists_For_File()
		{
			// If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata header 
			// and its value as specified by the Client during the creation.

			const string metadata = "filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==,othermeta c29tZSBvdGhlciBkYXRh";

			using (var server = TestServerFactory.Create(app =>
			{
				var store = Substitute.For<ITusStore, ITusCreationStore>();
				store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
				store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(100);
				store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(50);

				((ITusCreationStore)store).GetUploadMetadataAsync("testfile", Arg.Any<CancellationToken>()).Returns(metadata);

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

				response.ShouldContainHeader("Upload-Metadata", metadata);
			}
		}

		[Fact]
		public async Task Response_Does_Not_Contain_UploadMetadata_If_Metadata_Does_Not_Exist_For_File()
		{
			// If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata header 
			// and its value as specified by the Client during the creation.

			using (var server = TestServerFactory.Create(app =>
			{
				var store = Substitute.For<ITusStore, ITusCreationStore>();
				store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
				store.GetUploadLengthAsync("testfile", Arg.Any<CancellationToken>()).Returns(100);
				store.GetUploadOffsetAsync("testfile", Arg.Any<CancellationToken>()).Returns(50);

				((ITusCreationStore)store).GetUploadMetadataAsync("testfile", Arg.Any<CancellationToken>()).Returns(null as string);

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

				response.Headers.Contains("Upload-Metadata").ShouldBeFalse();
			}
		}
	}
}
