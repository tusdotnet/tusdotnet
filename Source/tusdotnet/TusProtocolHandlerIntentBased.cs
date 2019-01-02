using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet
{
    internal static class TusProtocolHandlerIntentBased
    {
        public static bool RequestIsForTusEndpoint(Uri requestUri, DefaultTusConfiguration configuration)
        {
            return requestUri.LocalPath.StartsWith(configuration.UrlPath, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<ResultType> Invoke(ContextAdapter context)
        {
            context.Configuration.Validate();

            var intentHandler = await IntentManager.DetermineIntent(context);

#warning TODO: Only place in code where ContinueExecution is returned? No need to return anything from the handlers in that case
            if (intentHandler == IntentHandler.NotApplicable)
                return ResultType.ContinueExecution;

            var onAuhorizeResult = await EventHelper.Validate<AuthorizeContext>(context, ctx =>
            {
                ctx.Intent = intentHandler.Intent;
            });

            if (onAuhorizeResult == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            InMemoryFileLock fileLock = null;

            if (intentHandler.LockType == LockType.RequiresLock)
            {
                fileLock = new InMemoryFileLock(context.GetFileId());

                var hasLock = fileLock.Lock();
                if (!hasLock)
                {
                    return await context.Response.ErrorResult(HttpStatusCode.Conflict,
                        $"File {context.GetFileId()} is currently being updated. Please try again later");
                }
            }

            try
            {
                if (!await intentHandler.Validate(context))
                {
                    return ResultType.StopExecution;
                }

                return await intentHandler.Invoke();
            }
            catch (TusStoreException storeException)
            {
                return await context.Response.ErrorResult(HttpStatusCode.BadRequest, storeException.Message);
            }
            finally
            {
                fileLock?.ReleaseIfHeld();
            }
        }
    }
}