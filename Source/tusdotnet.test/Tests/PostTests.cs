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
				tusStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("fileId");

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
				tusStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("fileId");

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

		[Fact]
		public async Task UploadMetadata_Is_Saved_If_Provided()
		{

			const string metadata = "filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==,othermeta c29tZSBvdGhlciBkYXRh";

			ITusCreationStore tusStore = null;

			using (var server = TestServer.Create(app =>
			{
				tusStore = Substitute.For<ITusCreationStore, ITusStore>();
				tusStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("fileId");

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
					.AddHeader("Upload-Metadata", metadata)
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.Created);

				var createFileAsyncCall = tusStore
					.ReceivedCalls()
					.FirstOrDefault(f => f.GetMethodInfo().Name == nameof(tusStore.CreateFileAsync));

				createFileAsyncCall.ShouldNotBeNull("No call to CreateFileAsync occurred.");
				// ReSharper disable once PossibleNullReferenceException
				createFileAsyncCall.GetArguments()[1].ShouldBe(metadata);
			}
		}

		[Fact]
		public async Task Returns_400_Bad_Request_If_UploadMetadata_Is_Empty()
		{
			// The Upload-Metadata request and response header MUST consist of one or more comma-separated key-value pairs. 
			using (var server = TestServer.Create(app =>
			{
				var tusStore = Substitute.For<ITusCreationStore, ITusStore>();
				tusStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("fileId");

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = tusStore as ITusStore,
					UrlPath = "/files"
				});
			}))
			{
				// Check empty header
				var response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.AddHeader("Upload-Metadata", "")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
				response.Content
					.ReadAsStringAsync()
					.Result
					.ShouldBe("Header Upload-Metadata must consist of one or more comma-separated key-value pairs");

			}
		}

		[Fact]
		public async Task Returns_400_Bad_Request_If_UploadMetadata_Is_In_An_Incorrect_Format()
		{
			// The key and value MUST be separated by a space.
			// The key MUST NOT contain spaces and commas and MUST NOT be empty. 
			using (var server = TestServer.Create(app =>
			{
				var tusStore = Substitute.For<ITusCreationStore, ITusStore>();
				tusStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("fileId");

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = tusStore as ITusStore,
					UrlPath = "/files"
				});
			}))
			{

				const string formatErrorMessage =
					"Header Upload-Metadata: The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs. The key and value MUST be separated by a space.The key MUST NOT contain spaces and commas and MUST NOT be empty. The key SHOULD be ASCII encoded and the value MUST be Base64 encoded.All keys MUST be unique.";

				const string emptyKeyErrorMessage = "Header Upload-Metadata: Key must not be empty";

				// Check header with only a key and no value
				var response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.AddHeader("Upload-Metadata", "somekey")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
				var message = await response.Content.ReadAsStringAsync();
				message.ShouldBe(formatErrorMessage);

				// Check empty key
				response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.AddHeader("Upload-Metadata", "somekey c29tZXZhbHVl, someotherkey")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
				message = await response.Content.ReadAsStringAsync();
				message.ShouldBe(emptyKeyErrorMessage);

				// Check missing key
				response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.AddHeader("Upload-Metadata", "   c29tZXZhbHVl")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
				message = await response.Content.ReadAsStringAsync();
				message.ShouldBe(formatErrorMessage);

				// Check header with space separated keys and values
				response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.AddHeader("Upload-Metadata", "somekey c29tZXZhbHVl someotherkey")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
				message = await response.Content.ReadAsStringAsync();
				message.ShouldBe(formatErrorMessage);

				// Check header with dash separated keys and values
				response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					.AddHeader("Upload-Metadata", "somekey c29tZXZhbHVl - someotherkey")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
				message = await response.Content.ReadAsStringAsync();
				message.ShouldBe(formatErrorMessage);
			}
		}

		[Fact]
		public async Task Returns_400_Bad_Request_If_UploadMetadata_Contains_Values_That_Are_Not_Base64_Encoded()
		{
			// The key SHOULD be ASCII encoded and the value MUST be Base64 encoded
			using (var server = TestServer.Create(app =>
			{
				var tusStore = Substitute.For<ITusCreationStore, ITusStore>();
				tusStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("fileId");

				app.UseTus(request => new DefaultTusConfiguration
				{
					Store = tusStore as ITusStore,
					UrlPath = "/files"
				});
			}))
			{
				// Check empty header
				var response = await server
					.CreateRequest("/files")
					.AddTusResumableHeader()
					.AddHeader("Upload-Length", "1")
					// Invalid base64, missing "==" at the end.
					.AddHeader("Upload-Metadata", "filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			}
		}

		[Fact]
		public async Task Returns_400_Bad_Request_Is_All_UploadMetadata_Keys_Are_Not_Unique()
		{
			// All keys MUST be unique.
			using (var server = TestServer.Create(app =>
			{
				var tusStore = Substitute.For<ITusCreationStore, ITusStore>();
				tusStore.CreateFileAsync(1, null, CancellationToken.None).ReturnsForAnyArgs("fileId");

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
					.AddHeader("Upload-Metadata",
						"filename d29ybGRfZG9taW5hdGlvbl9wbGFuLnBkZg==,filename dHVzZG90bmV0X2RvbWluYXRpb25fcGxhbi5wZGY=")
					.PostAsync();

				response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
				var message = await response.Content.ReadAsStringAsync();
				message.ShouldBe("Header Upload-Metadata: Duplicate keys are not allowed");
			}
		}
	}
}
