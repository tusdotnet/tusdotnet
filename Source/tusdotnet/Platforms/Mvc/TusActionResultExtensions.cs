#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Mvc;

namespace tusdotnet.Platforms.Mvc
{
    public static class TusActionResultExtensions
    {
        public static IActionResult ToActionResult(this TusHandleRequestResult result)
        {
            return result.IsHandled ? new TusActionResult(result) : new NotFoundResult();
        }
    }
}
#endif
