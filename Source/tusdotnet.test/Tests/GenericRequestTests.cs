using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.test.Extensions;
using Xunit;
using tusdotnet.Models.Configuration;
#if netfull
using Owin;
#endif
#if netstandard
using Microsoft.AspNetCore.Builder;
#endif

namespace tusdotnet.test.Tests
{
	public class GenericRequestTests
	{
        private bool _callForwarded;
        private bool _onAuthorizeWasCalled;

        [Fact]
		public async Task Ignores_Requests_Without_The_Tus_Resumable_Header()
		{
			using (var server = TestServerFactory.Create(app =>
			{
                app.UseTus(_ => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files",
                    Events = new Events
                    {
                        OnAuthorizeAsync = ctx =>
                        {
                            _onAuthorizeWasCalled = true;
                            return Task.FromResult(0);
                        }
                    }
                });

				app.Use((ctx, next) =>
				{
					_callForwarded = true;
					return Task.FromResult(true);
				});
			}))
			{
				await server.CreateRequest("/files").SendAsync("POST");
                AssertForwardCall(true);

				await server.CreateRequest("/files/testfile").SendAsync("HEAD");
                AssertForwardCall(true);

                // OPTIONS requests ignore the Tus-Resumable header according to spec.
                await server.CreateRequest("/files").SendAsync("OPTIONS");
                AssertForwardCall(false);
            }
		}

		[Fact]
		public async Task Ignores_Requests_Where_Method_Is_Not_Supported()
		{
			using (var server = TestServerFactory.Create(app =>
			{
				app.UseTus(_ => new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files",
                    Events = new Events
                    {
                        OnAuthorizeAsync = ctx =>
                        {
                            _onAuthorizeWasCalled = true; 
                            return Task.FromResult(0);
                        }
                    }
				});

				app.Use((ctx, next) =>
				{
					_callForwarded = true;
					return Task.FromResult(true);
				});
			}))
			{
                await server.CreateRequest("/files").AddTusResumableHeader().GetAsync();
				AssertForwardCall(true);

                await server.CreateRequest("/files/testfile").AddTusResumableHeader().GetAsync();
                AssertForwardCall(true);
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
		    var store = Substitute.For<ITusStore, ITusCreationStore, ITusTerminationStore>();
		    store.FileExistAsync("testfile", Arg.Any<CancellationToken>()).Returns(true);
            using (var server = TestServerFactory.Create(store))
            {
                var options = server.CreateRequest("/files").AddHeader("Tus-Resumable", version).SendAsync("OPTIONS");
                var post = server.CreateRequest("/files").AddHeader("Tus-Resumable", version).SendAsync("POST");
                var head = server.CreateRequest("/files/testfile").AddHeader("Tus-Resumable", version).SendAsync("HEAD");
                var patch = server.CreateRequest("/files/testfile").AddHeader("Tus-Resumable", version).SendAsync("PATCH");
                var delete = server.CreateRequest("/files/testfile").AddHeader("Tus-Resumable", version).SendAsync("DELETE");

                await Task.WhenAll(options, post, head, patch, delete);

                // Options does not care about the Tus-Resumable header according to spec.
                options.Result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                post.Result.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
                head.Result.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
                patch.Result.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
                delete.Result.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
            }
		}

        private void AssertForwardCall(bool expectedCallForwarded)
        {
            _callForwarded.ShouldBe(expectedCallForwarded);
            _onAuthorizeWasCalled.ShouldBe(!expectedCallForwarded);

            _onAuthorizeWasCalled = false;
            _callForwarded = false;
        }
    }
}
