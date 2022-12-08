using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;

namespace tusdotnet.Extensions.Internal
{
    internal static  class IntentHandlerExtensions
    {
        internal static async Task<ResultType> VerifyTusVersionIfApplicable(this IntentHandler handler, ContextAdapter context)
        {
            // Options does not require a correct tus resumable header.
            if (handler.Intent == IntentType.GetOptions)
                return ResultType.ContinueExecution;

            var tusResumableHeader = context.Request.Headers.TusResumable;

            if (tusResumableHeader == HeaderConstants.TusResumableValue)
                return ResultType.ContinueExecution;

            context.Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            context.Response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);
            await context.Response.Error(HttpStatusCode.PreconditionFailed, $"Tus version {tusResumableHeader} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");

            return ResultType.StopExecution;
        }
    }
}
