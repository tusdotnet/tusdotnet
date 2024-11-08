using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using tusdotnet.Interfaces;

namespace AspNetCore_net6._0_TestApp.Controllers
{
    public class Filter3AuthFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<
                ILogger<Filter3AuthFilter>
            >();

            logger.LogInformation("In IAuthorizationFilter");
        }
    }

    public class Filter1 : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next
        )
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Filter1>>();

            logger.LogInformation("In IAsyncActionFilter before controller action");

            await next();

            logger.LogInformation("In IAsyncActionFilter after controller action");
        }
    }

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
            if (data is not null)
            {
                _logger.LogInformation($"Content length is {data.DataLength}");
            }

            return Ok("hello world");
        }
    }
}
