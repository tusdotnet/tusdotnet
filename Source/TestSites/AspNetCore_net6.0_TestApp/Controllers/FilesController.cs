using Microsoft.AspNetCore.Mvc;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Platforms.Mvc;

namespace AspNetCore_net6._0_TestApp.Controllers
{
    [ApiController]
    [Route("/files-controller")]
    public class FilesController : Controller
    {
        private readonly ITusProtocolHandler _tusHandler;

        public FilesController(ITusProtocolHandler tusHandler)
        {
            _tusHandler = tusHandler;
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateUpload()
        {
            var result = await _tusHandler.HandleAsync(TusContext.From(HttpContext));
            return result.ToActionResult();
        }

        [HttpPatch("{fileId}")]
        public async Task<IActionResult> UploadData(string fileId)
        {
            var result = await _tusHandler.HandleAsync(TusContext.From(HttpContext));
            return result.ToActionResult();
        }

        [HttpOptions, HttpHead("{fileId}"), HttpDelete("{fileId}")]
        public async Task<IActionResult> HandleOtherStuff(string? fileId)
        {
            var result = await _tusHandler.HandleAsync(TusContext.From(HttpContext));
            return result.ToActionResult();
        }
    }
}
