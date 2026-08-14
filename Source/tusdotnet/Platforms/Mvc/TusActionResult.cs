#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using tusdotnet.Extensions;

namespace tusdotnet.Platforms.Mvc
{
    public class TusActionResult : IActionResult
    {
        private readonly TusHandleRequestResult _result;

        public TusActionResult(TusHandleRequestResult result)
        {
            _result = result;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            if (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                return;
            }

            context.HttpContext.Response.StatusCode = (int)_result.StatusCode;
            foreach (var item in _result.Headers)
            {
                context.HttpContext.Response.Headers[item.Key] = item.Value;
            }
            if (!string.IsNullOrWhiteSpace(_result.Message))
            {
                context.HttpContext.Response.ContentType = "text/plain";
                await _result.WriteMessageToStream(context.HttpContext.Response.Body);
            }
        }
    }
}
#endif
