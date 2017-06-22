using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
			using (var server = TestServerFactory.Create(app =>
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
					.And(m => m.AddBody())
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
				//store.DidNotReceive().GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>());
				store.DidNotReceive().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
#pragma warning restore 4014

			}
		}

		[Theory, XHttpMethodOverrideData]
		public async Task Returns_460_Checksum_Mismatch_If_The_Checksum_Does_Not_Match(string methodToUse)
		{
			using (var server = TestServerFactory.Create(app =>
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
					.And(m => m.AddBody())
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
			using (var server = TestServerFactory.Create(app =>
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
					.And(m => m.AddBody())
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
	    public async Task Returns_204_No_Content_If_Store_Supports_Checksum_But_No_Checksum_Is_Provided(string methodToUse)
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

            using (var server = TestServerFactory.Create(store))
            {
                var response = await server
                    .CreateRequest("/files/checksum")
                    .And(m => m.AddBody())
                    .AddTusResumableHeader()
                    .AddHeader("Upload-Offset", "5")
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
			using (var server = TestServerFactory.Create(app =>
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
				// ReSharper disable once LoopCanBePartlyConvertedToQuery - Only applies to netstandard
				foreach (var unparsables in new[] { "Kq5sNclPz7QV2+lfQIuc6R7oRu0=", "sha1 ", "", "sha1 Kq5sNclPz7QV2+lfQIuc6R7oRu0" })
				{

				#if netstandard

				// ASP.NET Core ignores empty headers so there is no way of knowing if the header was sent empty
				// or if the header is simply absent

					if (unparsables == "")
					{
						continue;
					}

				#endif

					var response = await server
					.CreateRequest("/files/checksum")
					.And(m => m.AddBody())
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
				//store.DidNotReceive().GetUploadLengthAsync("checksum", Arg.Any<CancellationToken>());
				store.DidNotReceive().AppendDataAsync("checksum", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
				cstore.DidNotReceive().GetSupportedAlgorithmsAsync(Arg.Any<CancellationToken>());
#pragma warning restore 4014

			}
		}
	}
}
