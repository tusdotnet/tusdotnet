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
            var multiIntent = await IntentAnalyzer.DetermineIntent(context);

            if (multiIntent is null)
            {
                return ResultType.ContinueExecution;
            }

            while (multiIntent.MoveNext() && multiIntent.Current is not null)
            {
                var handler = CreateHandlerWithEvents(multiIntent.Current);
                var result = await handler.RunWithEvents(context, swallowExceptionsDuringInvoke: multiIntent.Previous is not null);
                if (result == ResultType.StopExecution)
                    break;
            }

            await multiIntent.FinalizeResponse();

            return ResultType.StopExecution;
        }

        private static async Task<ResultType> RunWithEvents(this IntentHandlerWithEvents handler, ContextAdapter context, bool swallowExceptionsDuringInvoke)
        {
            var onAuhorizeResult = await handler.Authorize();

            if (onAuhorizeResult == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            if (handler.IntentHandler.VerifyTusVersionIfApplicable(context) == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            ITusFileLock? fileLock = null;

            if (handler.IntentHandler.LockType == LockType.RequiresLock)
            {
                fileLock = await context.GetFileLock();

                var hasLock = await fileLock.Lock();
                if (!hasLock)
                {
                    context.Response.Error(HttpStatusCode.Conflict, $"File {context.FileId} is currently being updated. Please try again later");
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
                context.Response.Error(HttpStatusCode.RequestEntityTooLarge, readSizeException.Message);
                return ResultType.StopExecution;
            }
            catch (TusStoreException storeException)
            {
                context.Response.Error(HttpStatusCode.BadRequest, storeException.Message);
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
    }
}
