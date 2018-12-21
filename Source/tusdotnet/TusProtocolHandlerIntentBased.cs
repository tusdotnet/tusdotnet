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

            if (intentHandler == IntentHandler.NotApplicable)
                return ResultType.NotHandled;

#warning TODO: await context.Raise<AuthorizeContext>(evnt => evnt => evnt.Intent = intentHandler.Intent);

            if (context.Configuration.Events?.OnAuthorize != null)
            {
                var authContext = AuthorizeContext.Create(context, evnt => evnt.Intent = intentHandler.Intent);
                await context.Configuration.Events.OnAuthorize(authContext);

                if (authContext.HasFailed)
                    return await context.Response.ErrorResult(HttpStatusCode.Unauthorized, authContext.ErrorMessage);
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
                    return ResultType.Handled;
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