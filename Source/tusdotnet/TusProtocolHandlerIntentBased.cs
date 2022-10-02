﻿using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Extensions.Internal;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Interfaces;
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
            var intentHandler = IntentAnalyzer.DetermineIntent(context);

            if (intentHandler == IntentHandler.NotApplicable)
            {
                return ResultType.ContinueExecution;
            }

            var onAuhorizeResult = await EventHelper.Validate<AuthorizeContext>(context, ctx =>
            {
                ctx.Intent = intentHandler.Intent;
                ctx.FileConcatenation = GetFileConcatenationFromIntentHandler(intentHandler);
            });

            if (onAuhorizeResult == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            if (await VerifyTusVersionIfApplicable(context, intentHandler) == ResultType.StopExecution)
            {
                return ResultType.StopExecution;
            }

            ITusFileLock fileLock = null;

            if (intentHandler.LockType == LockType.RequiresLock)
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
                if (!await intentHandler.Validate())
                {
                    return ResultType.StopExecution;
                }

                await intentHandler.Invoke();
                return ResultType.StopExecution;
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
            finally
            {
                fileLock?.ReleaseIfHeld();
            }
        }

        private static Models.Concatenation.FileConcat GetFileConcatenationFromIntentHandler(IntentHandler intentHandler)
        {
            return intentHandler is ConcatenateFilesHandler concatFilesHandler ? concatFilesHandler.UploadConcat.Type : null;
        }

        private static async Task<ResultType> VerifyTusVersionIfApplicable(ContextAdapter context, IntentHandler intentHandler)
        {
            // Options does not require a correct tus resumable header.
            if (intentHandler.Intent == IntentType.GetOptions)
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