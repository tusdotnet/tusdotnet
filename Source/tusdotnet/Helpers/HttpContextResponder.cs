using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;

namespace tusdotnet.Helpers
{
    internal static class HttphttpContextResponder
    {
        internal static async Task RespondToClient(this HttpContext httpContext, ResponseAdapter response)
        {
            // TODO: Implement support for custom responses by not writing if response has started

            httpContext.Response.StatusCode = (int)response.Status;
            httpContext.RespondToClientWithHeadersOnly(response);

            if (string.IsNullOrWhiteSpace(response.Message))
                return;

            httpContext.Response.ContentType = "text/plain";
            await response.WriteMessageToStream(httpContext.Response.Body);
        }

        internal static void RespondToClientWithHeadersOnly(this HttpContext httpContext, ResponseAdapter response)
        {
            foreach (var item in response.Headers)
            {
                httpContext.Response.Headers[item.Key] = item.Value;
            }
        }
    }
}
