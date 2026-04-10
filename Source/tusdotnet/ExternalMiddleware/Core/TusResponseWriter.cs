using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Extensions;

namespace tusdotnet.ExternalMiddleware.Core
{
    internal static class TusResponseWriter
    {
        internal static async Task WriteToResponse(TusHandleRequestResult result, HttpContext context)
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                context.Abort();
                return;
            }

            context.Response.StatusCode = (int)result.StatusCode;

            foreach (var item in result.Headers)
            {
                context.Response.Headers[item.Key] = item.Value;
            }

            if (string.IsNullOrWhiteSpace(result.Message))
                return;

            context.Response.ContentType = "text/plain";
            await result.WriteMessageToStream(context.Response.Body);
        }
    }
}
