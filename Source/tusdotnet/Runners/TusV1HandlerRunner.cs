#if NET6_0_OR_GREATER

#nullable enable

using Microsoft.Extensions.DependencyInjection;
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
using tusdotnet.Runners.Handlers;
using tusdotnet.Runners.TusV1Process;

namespace tusdotnet.Runners
{
    internal static class TusV1HandlerRunner<T> where T : TusV1Handler
    {
        internal static async Task<ResultType> Invoke(ContextAdapter context)
        {
            var multiIntent = await IntentAnalyzer.DetermineIntent(context);

            if (multiIntent is null)
            {
                return ResultType.ContinueExecution;
            }

            var handler = context.HttpContext.RequestServices.GetRequiredService<T>();

            await handler.Initialize();

            while (multiIntent.MoveNext() && multiIntent.Current is not null)
            {
                var result = await InvokeHandler(multiIntent.Current, handler, context, multiIntent.Previous is not null);
                if (result == ResultType.StopExecution)
                    break;
            }

            await handler.Finalize();

            await multiIntent.FinalizeResponse();

            return ResultType.StopExecution;
        }

        private static async Task<ResultType> InvokeHandler(IntentHandler intentHandler, T handler, ContextAdapter context, bool swallowExceptionsDuringInvoke)
        {
            // TODO: Dont create the handler before this has been checked. Use a Lazy<T>? 
            // TODO: This method and TusV1EventRunner.RunWithEvents are very similar. See if they can be merged. 
            if (intentHandler.VerifyTusVersionIfApplicable(context) == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            ITusFileLock? fileLock = null;

            if (intentHandler.LockType == LockType.RequiresLock)
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
                if (!await intentHandler.Validate())
                {
                    return ResultType.StopExecution;
                }

                await InvokeHandlerMethod(intentHandler, handler);
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

        private static async Task InvokeHandlerMethod(IntentHandler intentHandler, T handler)
        {
            TusV1Response? response = intentHandler switch
            {
                CreateFileHandler => await handler.CreateFile(CreateFileRequest.FromContextAdapter(intentHandler.Context)),
                WriteFileHandler => await handler.WriteFile(WriteFileRequest.FromContextAdapter(intentHandler.Context)),
                GetFileInfoHandler => await handler.GetFileInfo(FileInfoRequest.FromContextAdapter(intentHandler.Context)),
                DeleteFileHandler => await handler.DeleteFile(DeleteFileRequest.FromContextAdapter(intentHandler.Context)),
                _ => null
            };

            
            if (response is null)
                throw new NotImplementedException();

            response.CopyToCommonContext(intentHandler.Context);
        }
    }
}

#endif