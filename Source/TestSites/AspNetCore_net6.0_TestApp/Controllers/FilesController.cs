using Microsoft.AspNetCore.Mvc;
using tusdotnet;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;

namespace AspNetCore_net6._0_TestApp.Controllers
{
    [ApiController]
    [Route("/files-controller")]
    public class FilesController : Controller
    {
        private readonly ITusProtocolHandler _tusHandler;
        private static readonly Uri _endpointUrl = new("/files-controller", UriKind.Relative);

        public FilesController(ITusProtocolHandler tusHandler)
        {
            _tusHandler = tusHandler;
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateUpload()
        {
            var result = await _tusHandler.HandleAsync(HttpContext, _endpointUrl);
            return result.ToActionResult();
        }

        [HttpPatch("{fileId}")]
        public async Task<IActionResult> UploadData(string fileId)
        {
            var result = await _tusHandler.HandleAsync(HttpContext, _endpointUrl);
            return result.ToActionResult();
        }

        [HttpOptions, HttpHead("{fileId}"), HttpDelete("{fileId}")]
        public async Task<IActionResult> HandleOtherStuff(string? fileId)
        {
            var result = await _tusHandler.HandleAsync(HttpContext, _endpointUrl);
            return result.ToActionResult();
        }
    }

    public static class TusActionResultExtensions
    {
        public static IActionResult ToActionResult(this TusHandleRequestResult result)
        {
            if (!result.IsHandled)
                return new NotFoundResult();
            return new TusActionResult(result);
        }
    }

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
