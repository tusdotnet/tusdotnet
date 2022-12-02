#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Extensions.Internal;
using tusdotnet.IntentHandlers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Runners.Events;

namespace tusdotnet
{
    internal static class TusV1EventRunner
    {
        internal static async Task<ResultType> Invoke(ContextAdapter context)
        {
            var intentHandlers = await IntentAnalyzer.DetermineIntents(context);

            if (intentHandlers is null)
            {
                return ResultType.ContinueExecution;
            }

            IntentHandler? prev = null;

            foreach (var handlerAndResponse in intentHandlers)
            {
                var handler = CreateHandlerWithEvents(handlerAndResponse.Item1);
                context.Response = handlerAndResponse.Item2;

                if (prev is not null)
                {
                    IntentAnalyzer.ModifyContextForNextIntent(context, prev, handler.IntentHandler);
                }

                var singleResult = await RunSingleIntentHandler(context, handler, prev is not null);
                if (singleResult == ResultType.StopExecution)
                    break;

                prev = handler.IntentHandler;
            }

            context.Response = await IntentAnalyzer.MergeResponses(context, intentHandlers);

            return ResultType.StopExecution;
        }

        private static async Task<ResultType> RunSingleIntentHandler(ContextAdapter context, IntentHandlerWithEvents handler, bool swallowExceptionsDuringInvoke)
        {
            var onAuhorizeResult = await handler.Authorize();

            if (onAuhorizeResult == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            if (await VerifyTusVersionIfApplicable(context, handler) == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            ITusFileLock fileLock = null;

            if (handler.IntentHandler.LockType == LockType.RequiresLock)
            {
                fileLock = await context.GetFileLock();

                var hasLock = await fileLock.Lock();
                if (!hasLock)
                {
                    await context.Response.Error(HttpStatusCode.Conflict, $"File {context.FileId} is currently being updated. Please try again later");
                    return ResultType.StopExecution;
                }
            }

            try
            {
                if (!await handler.IntentHandler.Validate())
                {
                    return ResultType.StopExecution;
                }

                var validationResult = await handler.ValidateBeforeAction();
                if (validationResult == ResultType.StopExecution)
                {
                    return ResultType.StopExecution;
                }

                await handler.IntentHandler.Invoke();

                await handler.NotifyAfterAction();
            }
            catch (MaxReadSizeExceededException readSizeException)
            {
                await context.Response.Error(HttpStatusCode.RequestEntityTooLarge, readSizeException.Message);
                return ResultType.StopExecution;
            }
            catch (TusStoreException storeException)
            {
                await context.Response.Error(HttpStatusCode.BadRequest, storeException.Message);
                return ResultType.StopExecution;
            }
            catch (Exception) when (swallowExceptionsDuringInvoke)
            {
                // Left blank
            }
            finally
            {
                fileLock?.ReleaseIfHeld();
            }

            return ResultType.ContinueExecution;
        }

        private static IntentHandlerWithEvents CreateHandlerWithEvents(IntentHandler handler)
        {
            return handler switch
            {
                ConcatenateFilesHandler => new ConcatenateFilesHandlerWithEvents(handler),
                CreateFileHandler => new CreateFileHandlerWithEvents(handler),
                DeleteFileHandler => new DeleteFileHandlerWithEvents(handler),
                GetFileInfoHandler => new GetFileInfoHandlerWithEvents(handler),
                GetOptionsHandler => new GetOptionsHandlerWithEvents(handler),
                WriteFileHandler => new WriteFileHandlerWithEvents(handler),
                _ => throw new NotImplementedException()
            };
        }

        private static async Task<ResultType> VerifyTusVersionIfApplicable(ContextAdapter context, IntentHandlerWithEvents handler)
        {
            // Options does not require a correct tus resumable header.
            if (handler.IntentHandler.Intent == IntentType.GetOptions)
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
