using Microsoft.AspNetCore.Mvc;
using tusdotnet.Interfaces;

namespace AspNetCore_net6._0_TestApp.Controllers
{
    public class UploadFileController : Controller
    {
        private readonly ILogger<UploadFileController> _logger;
        private readonly ITusTerminationStore? _store;

        public UploadFileController(ILogger<UploadFileController> logger, ITusStore store)
        {
            _logger = logger;
            _store = store as ITusTerminationStore;
        }

        [HttpGet("/filesmodelbindingmvc")]
        public IActionResult Download()
        {
            return Ok("hej");
        }

        [Route("/filesmodelbindingmvc")]
        public async Task<IActionResult> Handle(MyMappedResumableUpload? data)
        {
            _logger.LogInformation($"MVC bound file to: {data?.UploadId ?? "<not bound>"}");

            if (data is not null)
            {
                _logger.LogInformation($"Content length is {data.DataLength}");
                _logger.LogInformation($"Number of metadata keys is {data.Metadata.Count} with name being {data.FileName}");
            }

            return Ok("hello world");
        }

    }
}
