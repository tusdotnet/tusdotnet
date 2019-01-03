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
        #warning TODO Throw better exception if configuration is null
            context.Configuration.Validate();

            var intentHandler = await IntentAnalyzer.DetermineIntent(context);

            if (intentHandler == IntentHandler.NotApplicable)
            {
                return ResultType.ContinueExecution;
            }

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
                    await context.Response.Error(HttpStatusCode.Conflict, $"File {context.GetFileId()} is currently being updated. Please try again later");
                    return ResultType.StopExecution;
                }
            }

            try
            {
                if (!await intentHandler.Validate(context))
                {
                    return ResultType.StopExecution;
                }

                await intentHandler.Invoke();
                return ResultType.StopExecution;
            }
            catch (TusStoreException storeException)
            {
                await context.Response.Error(HttpStatusCode.BadRequest, storeException.Message);
                return ResultType.StopExecution;
            }
            finally
            {
                fileLock?.ReleaseIfHeld();
            }
        }
    }
}