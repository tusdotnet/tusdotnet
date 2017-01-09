using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Testing;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Data;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
	public class ChecksumTests
	{
		[Theory, XHttpMethodOverrideData]
		public async Task Returns_400_Bad_Request_If_Checksum_Algorithm_Is_Not_Supported(string methodToUse)
		{
			var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
			using (var server = TestServer.Create(app =>
			{
				// ReSharper disable once SuspiciousTypeConversion.Global
				var cstore = (ITusChecksumStore)store;
				cstore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "md5" });

				store.FileExistAsync("checksum", CancellationToken.None).ReturnsForAnyArgs(true);
				store.GetUploadOffsetAsync("checksum", Arg.Any<CancellationToken>()).Returns(5);
				store.GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>()).Returns(10);
				store.AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files/checksum")
					.And(AddBody)
					.AddTusResumableHeader()
					.AddHeader("Upload-Offset", "5")
					.AddHeader("Upload-Checksum", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=")
					.OverrideHttpMethodIfNeeded("PATCH", methodToUse)
					.SendAsync(methodToUse);

				await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
					"Unsupported checksum algorithm. Supported algorithms are: md5");
				response.ShouldContainTusResumableHeader();

#pragma warning disable 4014
				store.DidNotReceive().FileExistAsync(null, CancellationToken.None);
				store.DidNotReceive().GetUploadOffsetAsync("checksum", Arg.Any<CancellationToken>());
				store.DidNotReceive().GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>());
				store.DidNotReceive().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
#pragma warning restore 4014

			}
		}

		[Theory, XHttpMethodOverrideData]
		public async Task Returns_460_Checksum_Mismatch_If_The_Checksum_Does_Not_Match(string methodToUse)
		{
			using (var server = TestServer.Create(app =>
			{
				var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
				store.FileExistAsync("checksum", CancellationToken.None).ReturnsForAnyArgs(true);
				store.GetUploadOffsetAsync("checksum", Arg.Any<CancellationToken>()).Returns(5);
				store.GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>()).Returns(10);
				store.AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

				// ReSharper disable once SuspiciousTypeConversion.Global
				var cstore = (ITusChecksumStore)store;
				cstore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });
				cstore.VerifyChecksumAsync(null, null, null, CancellationToken.None).ReturnsForAnyArgs(false);

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files/checksum")
					.And(AddBody)
					.AddTusResumableHeader()
					.AddHeader("Upload-Offset", "5")
					.AddHeader("Upload-Checksum", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=")
					.OverrideHttpMethodIfNeeded("PATCH", methodToUse)
					.SendAsync(methodToUse);

				await response.ShouldBeErrorResponse((HttpStatusCode)460,
					"Header Upload-Checksum does not match the checksum of the file");
				response.ShouldContainTusResumableHeader();
			}
		}

		[Theory, XHttpMethodOverrideData]
		public async Task Returns_204_No_Content_If_Checksum_Matches(string methodToUse)
		{
			using (var server = TestServer.Create(app =>
			{
				var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
				store.FileExistAsync("checksum", CancellationToken.None).ReturnsForAnyArgs(true);
				store.GetUploadOffsetAsync("checksum", Arg.Any<CancellationToken>()).Returns(5);
				store.GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>()).Returns(10);
				store.AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);
				// ReSharper disable once SuspiciousTypeConversion.Global
				var cstore = (ITusChecksumStore)store;
				cstore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "sha1" });
				cstore.VerifyChecksumAsync(null, null, null, CancellationToken.None).ReturnsForAnyArgs(true);

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});
			}))
			{
				var response = await server
					.CreateRequest("/files/checksum")
					.And(AddBody)
					.AddTusResumableHeader()
					.AddHeader("Upload-Offset", "5")
					.AddHeader("Upload-Checksum", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=")
					.OverrideHttpMethodIfNeeded("PATCH", methodToUse)
					.SendAsync(methodToUse);

				response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
				response.ShouldContainTusResumableHeader();
			}
		}

		[Theory, XHttpMethodOverrideData]
		public async Task Returns_400_Bad_Request_If_Upload_Checksum_Header_Is_Unparsable(string methodToUse)
		{
			var store = Substitute.For<ITusStore, ITusCreationStore, ITusChecksumStore>();
			ITusChecksumStore cstore = null;
			using (var server = TestServer.Create(app =>
			{
				// ReSharper disable once SuspiciousTypeConversion.Global
				cstore = (ITusChecksumStore)store;
				cstore.GetSupportedAlgorithmsAsync(CancellationToken.None).ReturnsForAnyArgs(new[] { "md5" });
				store.FileExistAsync("checksum", CancellationToken.None).ReturnsForAnyArgs(true);
				store.GetUploadOffsetAsync("checksum", Arg.Any<CancellationToken>()).Returns(5);
				store.GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>()).Returns(10);
				store.AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(5);

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = store,
					UrlPath = "/files"
				});
			}))
			{
				foreach (var unparsables in new[] { "Kq5sNclPz7QV2+lfQIuc6R7oRu0=", "sha1 ", "", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0" })
				{
					var response = await server
					.CreateRequest("/files/checksum")
					.And(AddBody)
					.AddTusResumableHeader()
					.AddHeader("Upload-Offset", "5")
					.AddHeader("Upload-Checksum", unparsables)
					.OverrideHttpMethodIfNeeded("PATCH", methodToUse)
					.SendAsync(methodToUse);

					await response.ShouldBeErrorResponse(HttpStatusCode.BadRequest,
						"Could not parse Upload-Checksum header");
					response.ShouldContainTusResumableHeader();
				}

#pragma warning disable 4014
				store.DidNotReceive().FileExistAsync("checksum", Arg.Any<CancellationToken>());
				store.DidNotReceive().GetUploadOffsetAsync("checksum", Arg.Any<CancellationToken>());
				store.DidNotReceive().GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>());
				store.DidNotReceive().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
				cstore.DidNotReceive().GetSupportedAlgorithmsAsync(Arg.Any<CancellationToken>());
#pragma warning restore 4014

			}
		}

		private static void AddBody(HttpRequestMessage message)
		{
			message.Content = new ByteArrayContent(new byte[] { 0, 0, 0 });
			message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
		}

		// TODO: Investigate if OWIN supports trailing headers.

		/*
		 * The Client and the Server MAY implement and use this extension to verify data integrity of each PATCH request. 
		 * If supported, the Server MUST add checksum to the Tus-Extension header.

	A Client MAY include the Upload-Checksum header in a PATCH request. 
	Once the entire request has been received, the Server MUST verify the uploaded chunk against the 
	provided checksum using the specified algorithm. 
	Depending on the result the Server MAY respond with one of the following status code: 
	1) 400 Bad Request if the checksum algorithm is not supported by the server, 
	2) 460 Checksum Mismatch if the checksums mismatch or 
	3) 204 No Content if the checksums match and the processing of the data succeeded. 
	
	In the first two cases the uploaded chunk MUST be discarded, and the upload and its offset MUST NOT be updated.

	The Server MUST support at least the SHA1 checksum algorithm identified by sha1. 
	The names of the checksum algorithms MUST only consist of ASCII characters with the modification that uppercase characters are excluded.

	The Tus-Checksum-Algorithm header MUST be included in the response to an OPTIONS request.

	If the hash cannot be calculated at the beginning of the upload, it MAY be included as a trailer. 
	If the Server can handle trailers, this behavior MUST be announced by adding checksum-trailer to the Tus-Extension header.
	 Trailers, also known as trailing headers, are headers which are sent after the request’s body has been transmitted already. 
	 Following RFC 7230 they MUST be announced using the Trailer header and are only allowed in chunked transfers.
		 * 
		 * 
		 * 
		 * Headers

Tus-Checksum-Algorithm

The Tus-Checksum-Algorithm response header MUST be a comma-separated list of the checksum algorithms supported by the server.

Upload-Checksum

The Upload-Checksum request header contains information about the checksum of the current body payload. 
The header MUST consist of the name of the used checksum algorithm and the Base64 encoded checksum separated by a space.

Example

Request:

OPTIONS /files HTTP/1.1
Host: tus.example.org
Tus-Resumable: 1.0.0
Response:

HTTP/1.1 204 No Content
Tus-Resumable: 1.0.0
Tus-Version: 1.0.0
Tus-Extension: checksum
Tus-Checksum-Algorithm: md5,sha1,crc32
Request:

PATCH /files/17f44dbe1c4bace0e18ab850cf2b3a83 HTTP/1.1
Content-Length: 11
Upload-Offset: 0
Tus-Resumable: 1.0.0
Upload-Checksum: sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0=

hello world
Response:

HTTP/1.1 204 No Content
Tus-Resumable: 1.0.0
Upload-Offset: 11
		 * 
		 * */
	}
}
