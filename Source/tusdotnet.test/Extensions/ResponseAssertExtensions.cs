using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Shouldly;

namespace tusdotnet.test.Extensions
{
	internal static class ResponseAssertHelpers
	{
		internal static async Task ShouldBeErrorResponse(this HttpResponseMessage response, HttpStatusCode expectedStatusCode,
			string expectedMessage = null)
		{
			response.StatusCode.ShouldBe(expectedStatusCode);
			response.Content.Headers.ContentType.MediaType.ShouldBe("text/plain");
			var body = await response.Content.ReadAsStringAsync();
			body.ShouldBe(expectedMessage);
		}

		internal static void ShouldContainTusResumableHeader(this HttpResponseMessage response)
		{
			response.ShouldContainHeader("Tus-Resumable", "1.0.0");
		}

		internal static void ShouldContainHeader(this HttpResponseMessage response, string headerName, string headerValue)
		{
			response.Headers.Contains(headerName).ShouldBeTrue("Header "  + headerName + " was not found in response");
			var value = response.Headers.GetValues(headerName).ToList();
			value.Count.ShouldBe(1);
			value[0].ShouldBe(headerValue);
		}

        internal static void ShouldNotContainHeaders(this HttpResponseMessage response, params string[] headerNames)
        {
            var allHeaders = response.Headers.Concat(response.Content?.Headers).ToDictionary(f => f.Key, null);

            foreach (var item in headerNames)
            {
                allHeaders.ContainsKey(item).ShouldBeFalse(item + " existed in response but should not have");
            }
        }
	}
}
