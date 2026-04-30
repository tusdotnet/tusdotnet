#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Exceptions;
using tusdotnet.Extensions;
using tusdotnet.Extensions.Internal;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
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

            LockingContext? lockingContext = null;

            if (handler.IntentHandler.LockType == LockType.RequiresLock)
            {
                try
                {
                    lockingContext = await TryAcquireLockingContext(context);
                }
                catch (TimeoutException)
                {
                    context.Response.Locked();
                    return ResultType.StopExecution;
                }

                catch (LockAcquisitionTimeoutException)
                {
                    context.Response.Locked();
                    return ResultType.StopExecution;
                }

                if (!lockingContext.IsAcquired)
                {
                    context.Response.Locked();
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

                // Disable exception swallowing so user exceptions in NotifyAfter propagate correctly.
                swallowExceptionsDuringInvoke = false;

                await handler.NotifyAfterAction();
            }
            catch (OperationCanceledException)
                when (context.CancellationToken.IsCancellationRequested)
            {
                // Client disconnected or preempted by a newer request.
                // GuardedToken (= httpContext.RequestAborted) is cancelled in both cases,
                // so the middleware will abort the connection instead of writing a response.
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
                if (lockingContext != null)
                {
                    await lockingContext.ReleaseIfHeld();
                }
            }

            return ResultType.ContinueExecution;
        }

        private static async Task<LockingContext> TryAcquireLockingContext(ContextAdapter context)
        {
            var uploadManager = context.Configuration.OngoingUploadManager;
            if (uploadManager is null)
            {
                var fileLock = await context.GetFileLock();
                var hasLock = await fileLock.Lock();

                return new LockingContext
                {
                    IsAcquired = hasLock,
                    ReleaseIfHeld = fileLock.ReleaseIfHeld,
                };
            }

            var ongoingUpload = await uploadManager.AcquireAsync(context.FileId);

            // Register a callback so that if this upload is preempted by a newer request,
            // the current request's cancellation token is cancelled. The registration is
            // disposed before release to avoid triggering on normal completion.
            var preemptionRegistration = ongoingUpload.CancellationToken.Register(
                static state => ((ContextAdapter)state!).CancelRequest(),
                context
            );

            return new LockingContext
            {
                IsAcquired = true,
                ReleaseIfHeld = async () =>
                {
                    // Dispose the registration before releasing so that the cancellation
                    // triggered by ReleaseAsync does not fire CancelRequest on this request.
                    preemptionRegistration.Dispose();
                    await uploadManager.ReleaseAsync(ongoingUpload);
                },
            };
        }

        private sealed class LockingContext
        {
            internal bool IsAcquired { get; set; }

            internal Func<Task> ReleaseIfHeld { get; set; } = () => TaskHelper.Completed;
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
