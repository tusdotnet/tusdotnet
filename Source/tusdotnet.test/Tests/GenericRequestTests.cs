using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin.Testing;
using NSubstitute;
using Owin;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests
{
	public class GenericRequestTests
	{
		[Fact]
		public async Task Ignores_Requests_Without_The_Tus_Resumable_Header()
		{
			var callForwarded = false;
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(() =>
				{
					var tusConfiguration = Substitute.For<ITusConfiguration>();
					tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
					tusConfiguration.UrlPath.Returns("/files");
					return tusConfiguration;
				});

				app.Use((ctx, next) =>
				{
					callForwarded = true;
					return Task.FromResult(true);
				});

			}))
			{
				await server.CreateRequest("/files").SendAsync("POST");
				callForwarded.ShouldBeTrue();
				callForwarded = false;
				await server.CreateRequest("/files/testfile").SendAsync("HEAD");
				callForwarded.ShouldBeTrue();
				callForwarded = false;
				await server.CreateRequest("/files").SendAsync("POST");
				callForwarded.ShouldBeTrue();
				callForwarded = false;

				// OPTIONS requests ignore the Tus-Resumable header according to spec.
				await server.CreateRequest("/files").SendAsync("OPTIONS");
				callForwarded.ShouldBeFalse();
			}
		}

		[Fact]
		public async Task Ignores_Requests_Where_Method_Is_Not_Supported()
		{
			var callForwarded = false;
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(() =>
				{
					var tusConfiguration = Substitute.For<ITusConfiguration>();
					tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
					tusConfiguration.UrlPath.Returns("/files");
					return tusConfiguration;
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
					.AddTusResumableHeader()
					.GetAsync();

				callForwarded.ShouldBeTrue();
				callForwarded = false;
				await server
					.CreateRequest("/files/testfile")
					.AddTusResumableHeader()
					.GetAsync();
			}
		}

		[Theory]
		[InlineData("0")]
		[InlineData("0.1b")]
		[InlineData("0.0.2")]
		[InlineData("1.0.1")]
		[InlineData("1.1.1")]
		[InlineData("1.0.0b")]
		public async Task Returns_412_Precondition_Failed_If_Tus_Resumable_Does_Not_Match_The_Supported_Version(string version)
		{
			using (var server = TestServer.Create(app =>
			{
				app.UseTus(() =>
				{
					var tusConfiguration = Substitute.For<ITusConfiguration>();
					tusConfiguration.Store.Returns(Substitute.For<ITusStore>());
					tusConfiguration.UrlPath.Returns("/files");
					return tusConfiguration;
				});

			}))
			{
				var options = server.CreateRequest("/files").AddHeader("Tus-Resumable", version).SendAsync("OPTIONS");
				var post = server.CreateRequest("/files").AddHeader("Tus-Resumable", version).SendAsync("POST");
				var head = server.CreateRequest("/files/testfile").AddHeader("Tus-Resumable", version).SendAsync("HEAD");
				var patch = server.CreateRequest("/files/testfile").AddHeader("Tus-Resumable", version).SendAsync("PATCH");

				await Task.WhenAll(options, post, head, patch);

				// Options does not care about the Tus-Resumable header according to spec.
				options.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
				post.Result.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
				head.Result.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
				patch.Result.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
			}
		}
	}
}
