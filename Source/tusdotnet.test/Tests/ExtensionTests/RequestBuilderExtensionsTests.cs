using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using tusdotnet.test.Extensions;
using Xunit;

namespace tusdotnet.test.Tests.ExtensionTests
{
    public class RequestBuilderExtensionsTests
	{
		[Fact]
		public async Task Should_Set_Tus_Resumable_Header_Properly()
		{
            using var server = TestServerFactory.Create(app => { });

            await server.CreateRequest("/")
                .And(r => r.Headers.Contains("Tus-Resumable").ShouldBeFalse())
                .AddTusResumableHeader()
                .And(r =>
                {
                    r.Headers.Contains("Tus-Resumable").ShouldBeTrue();
                    r.Headers.GetValues("Tus-Resumable").Count().ShouldBe(1);
                    r.Headers.GetValues("Tus-Resumable").First().ShouldBe("1.0.0");
                })
                .GetAsync();
        }

		[Theory]
		[InlineData("a", "b")]
		[InlineData("b", "a")]
		[InlineData("a", "a")]
		public async Task Should_Set_XHttpMethodOverride_Properly(string @override, string method)
		{
            using var server = TestServerFactory.Create(app => { });

            await server.CreateRequest("/")
                .And(r => r.Headers.Contains("X-Http-Method-Override").ShouldBeFalse())
                .OverrideHttpMethodIfNeeded(@override, method)
                .And(r =>
                {
                    if (@override == method)
                    {
                        r.Headers.Contains("X-Http-Method-Override").ShouldBeFalse();
                    }
                    else
                    {
                        r.Headers.Contains("X-Http-Method-Override").ShouldBeTrue();
                        r.Headers.GetValues("X-Http-Method-Override").Count().ShouldBe(1);
                        r.Headers.GetValues("X-Http-Method-Override").First().ShouldBe(@override);
                    }
                })
                .GetAsync();
        }
	}
}
