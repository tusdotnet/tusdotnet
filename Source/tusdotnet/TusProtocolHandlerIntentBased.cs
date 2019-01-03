using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
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

            var intentHandler = IntentAnalyzer.DetermineIntent(context);

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

            if (IntentRequiresTusResumableHeader(intentHandler.Intent))
            {
                var tusResumableHeader = context.Request.GetHeader(HeaderConstants.TusResumable);

                if (tusResumableHeader == null)
                    return ResultType.ContinueExecution;

                if (await ValidateTusResumableHeader(context.Response, tusResumableHeader) == ResultType.StopExecution)
                    return ResultType.StopExecution;
            }

            InMemoryFileLock fileLock = null;

            if (intentHandler.LockType == LockType.RequiresLock)
            {
                fileLock = new InMemoryFileLock(context.GetFileId());

                var hasLock = fileLock.Lock();
                if (!hasLock)
                {
                    await context.Response.Error(HttpStatusCode.Conflict, $"File {context.Request.FileId} is currently being updated. Please try again later");
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

        private static bool IntentRequiresTusResumableHeader(IntentType intent)
        {
            return intent != IntentType.GetOptions;
        }

        private static async Task<ResultType> ValidateTusResumableHeader(ResponseAdapter response, string tusResumableHeader)
        {
            if (tusResumableHeader == HeaderConstants.TusResumableValue)
                return ResultType.ContinueExecution;

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);
            await response.Error(HttpStatusCode.PreconditionFailed,
                $"Tus version {tusResumableHeader} is not supported. Supported versions: {HeaderConstants.TusResumableValue}");
            return ResultType.StopExecution;
        }
    }
}