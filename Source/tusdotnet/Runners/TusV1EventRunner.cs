#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
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
                var result = await handler.RunWithEvents(
                    context,
                    swallowExceptionsDuringInvoke: multiIntent.Previous is not null
                );
                if (result == ResultType.StopExecution)
                    break;
            }

            await multiIntent.FinalizeResponse();

            return ResultType.StopExecution;
        }

        private static async Task<ResultType> RunWithEvents(
            this IntentHandlerWithEvents handler,
            ContextAdapter context,
            bool swallowExceptionsDuringInvoke
        )
        {
            var onAuthorizeResult = await handler.Authorize();

            if (onAuthorizeResult == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            if (
                handler.IntentHandler.VerifyTusVersionIfApplicable(context)
                == ResultType.StopExecution
            )
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
                    context.Response.Locked();
                    return ResultType.StopExecution;
                }

                if (fileLock is ITusLeasedFileLock leasedFileLock)
                {
                    context.FileLockLostToken = leasedFileLock.LockLostToken;
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

                // Invoke of generic code was OK so disable the swallowing of exceptions to allow propagation of
                // user thrown exceptions in NotifyAfter.
                swallowExceptionsDuringInvoke = false;

                await handler.NotifyAfterAction();
            }
            catch (OperationCanceledException)
                when (context.CancellationToken.IsCancellationRequested)
            {
                // Client disconnected - just stop execution without error response
                return ResultType.StopExecution;
            }
            catch (OperationCanceledException)
                when (context.FileLockLostToken.IsCancellationRequested)
            {
                // The store stopped because the lock was lost. Another request may own the file now,
                // so the client must re-read the offset instead of trusting what this request wrote.
                context.Response.UploadConflict(
                    "The file lock was lost while the file was being updated. Please try again"
                );
                return ResultType.StopExecution;
            }
            catch (TusUploadConflictException conflictException)
            {
                context.Response.UploadConflict(conflictException.Message);
                return ResultType.StopExecution;
            }
            catch (MaxReadSizeExceededException readSizeException)
            {
                context.Response.Error(
                    HttpStatusCode.RequestEntityTooLarge,
                    readSizeException.Message
                );
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
                if (fileLock != null)
                {
                    // The context is reused by the next intent (e.g. creation-with-upload) which acquires its own lock.
                    context.FileLockLostToken = default;
                    await fileLock.ReleaseIfHeld();
                }
            }

            return ResultType.ContinueExecution;
        }

        private static IntentHandlerWithEvents CreateHandlerWithEvents(IntentHandler handler)
        {
            return handler switch
            {
                ConcatenateFilesHandler concatenateHandler => new ConcatenateFilesHandlerWithEvents(
                    concatenateHandler
                ),
                CreateFileHandler createHandler => new CreateFileHandlerWithEvents(createHandler),
                DeleteFileHandler deleteHandler => new DeleteFileHandlerWithEvents(deleteHandler),
                GetFileInfoHandler getInfoHandler => new GetFileInfoHandlerWithEvents(
                    getInfoHandler
                ),
                GetOptionsHandler getOptionsHandler => new GetOptionsHandlerWithEvents(
                    getOptionsHandler
                ),
                WriteFileHandler writeHandler => new WriteFileHandlerWithEvents(writeHandler),
                _ => throw new NotImplementedException(),
            };
        }
    }
}
