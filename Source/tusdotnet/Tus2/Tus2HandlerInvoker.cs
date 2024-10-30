﻿using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using tusdotnet.Models;
using tusdotnet.Models.PipeReaders;

namespace tusdotnet.Tus2
{
    internal class Tus2HandlerInvoker
    {
        internal static async Task<UploadRetrievingProcedureResponse> RetrieveOffsetEntryPoint(
            TusHandler handler,
            RetrieveOffsetContext context,
            IOngoingUploadManager uploadManager
        )
        {
            /*
            If an upload is interrupted, the client MAY attempt to fetch the offset of the incomplete upload by sending a HEAD request to the server with the same Upload-Token header field ({{upload-token}}). The client MUST NOT initiate this procedure without the knowledge of server support.

            The request MUST use the HEAD method and include the Upload-Token header. The request MUST NOT include the Upload-Offset header or the Upload-Incomplete header. The server MUST reject the request with the Upload-Offset header or the Upload-Incomplete header by sending a 400 (Bad Request) response.

            If the server considers the upload associated with this token active, it MUST send back a 204 (No Content) response. The response MUST include the Upload-Offset header set to the current resumption offset for the client. The response MUST include the Upload-Incomplete header which is set to true if and only if the upload is incomplete. An upload is considered complete if and only if the server completely and succesfully received a corresponding Upload Transfer Procedure ({{upload-transfer}}) request with the Upload-Incomplete header being omitted or set to false.

            The client MUST NOT perform the Offset Retrieving Procedure ({{offset-retrieving}}) while the Upload Transfer Procedures ({{upload-transfer}}) is in progress.

            The offset MUST be accepted by a subsequent Upload Transfer Procedure ({{upload-transfer}}). Due to network delay and reordering, the server might still be receiving data from an ongoing transfer for the same token, which in the client perspective has failed. The server MAY terminate any transfers for the same token before sending the response by abruptly terminating the HTTP connection or stream. Alternatively, the server MAY keep the ongoing transfer alive but ignore further bytes received past the offset.

            The client MUST NOT start more than one Upload Transfer Procedures ({{upload-transfer}}) based on the resumption offset from a single Offset Retrieving Procedure ({{offset-retrieving}}).

            The response SHOULD include Cache-Control: no-store header to prevent HTTP caching.

            If the server does not consider the upload associated with this token active, it MUST respond with 404 (Not Found) status code.

            The client MAY automatically start uploading from the beginning using Upload Transfer Procedure ({{upload-transfer}}) if 404 (Not Found) status code is received. The client SHOULD NOT automatically retry if a status code other than 204 and 404 is received.

            * */

            var storageFacade = await handler.GetStorageFacade();

            try
            {
                Tus2Validator.AssertNoInvalidHeaders(context.Headers);

                await uploadManager.CancelOtherUploads(context.Headers.ResourceId);

                await Tus2Validator.AssertFileExist(
                    storageFacade.Storage,
                    context.Headers.ResourceId
                );

                var response = await handler.RetrieveOffset(context);
                response.ContentLocation = await handler.GetContentLocation(
                    context.Headers.ResourceId,
                    !response.UploadComplete
                );

                response.UploadLimit = handler.Limits;

                return response;
            }
            catch (Tus2AssertRequestException ex)
            {
                return new()
                {
                    Status = ex.Status,
                    ErrorMessage = ex.ErrorMessage,
                    UploadOffset = await TryGetOffset(
                        storageFacade.Storage,
                        context.Headers.ResourceId!
                    )
                };
            }
        }

        internal static async Task<UploadCancellationProcedureResponse> DeleteEntryPoint(
            TusHandler handler,
            DeleteContext context,
            IOngoingUploadManager uploadManager
        )
        {
            /*
            If the client wants to terminate the transfer without the ability to resume, it MAY send a DELETE request to the server along with the Upload-Token which is an indication that the client is no longer interested in uploading this body and the server can release resources associated with this token. The client MUST NOT initiate this procedure without the knowledge of server support.

            The request MUST use the DELETE method and include the Upload-Token header. The request MUST NOT include the Upload-Offset header or the Upload-Incomplete header. The server MUST reject the request with the Upload-Offset header or the Upload-Incomplete header by sending a 400 (Bad Request) response.

            If the server has successfully released the resources allocated for this token, it MUST send back a 204 (No Content) response.

            The server MAY terminate any ongoing Upload Transfer Procedure ({{upload-transfer}}) for the same token before sending the response by abruptly terminating the HTTP connection or stream.

            If the server has no record of the token in Upload-Token, it MUST respond with 404 (Not Found) status code.

            If the server does not support cancellation, it MUST respond with 405 (Method Not Allowed) status code.
             * */


            var storageFacade = await handler.GetStorageFacade();

            try
            {
                if (!handler.AllowClientToDeleteFile)
                {
                    return new UploadCancellationProcedureResponse
                    {
                        Status = HttpStatusCode.MethodNotAllowed
                    };
                }

                Tus2Validator.AssertNoInvalidHeaders(context.Headers);

                await uploadManager.CancelOtherUploads(context.Headers.ResourceId);

                await Tus2Validator.AssertFileExist(
                    storageFacade.Storage,
                    context.Headers.ResourceId
                );

                return await handler.Delete(context);
            }
            catch (Tus2AssertRequestException ex)
            {
                return new()
                {
                    Status = ex.Status,
                    ErrorMessage = ex.ErrorMessage,
                    UploadOffset = await TryGetOffset(
                        storageFacade.Storage,
                        context.Headers.ResourceId!
                    )
                };
            }
        }

        public static async Task<UploadTransferProcedureResponse> WriteDataEntryPoint(
            TusHandler handler,
            WriteDataContext context,
            IOngoingUploadManager uploadManager
        )
        {
            // This method is a combo of the upload creation procedure and the upload appening procedure due to them being one and the same in earlier drafts.
            long? uploadOffsetFromStorage = null;
            try
            {
                var metadataParser =
                    context.HttpContext.RequestServices.GetRequiredService<IMetadataParser>();

                await uploadManager.CancelOtherUploads(context.Headers.ResourceId);

                var ongoingCancellationToken = await uploadManager.StartUpload(
                    context.Headers.ResourceId
                );
                await using var finishOngoing = Deferrer.Defer(
                    () => uploadManager.FinishUpload(context.Headers.ResourceId)
                );

                var metadata = metadataParser?.Parse(context.HttpContext);

                var storageFacade = await handler.GetStorageFacade();

                context.Headers.UploadOffset ??= 0;

                var contentLength =
                    context.Headers.UploadComplete != false ? context.Headers.ContentLength : null;

                var fileExist = await Tus2Validator.AssertFileExist(
                    storageFacade.Storage,
                    context.Headers.ResourceId,
                    context.Headers.UploadOffset != 0
                );

                if (!fileExist)
                {
                    long? resourceLengthForCreate = AssertAndGetResourceLength(
                        context,
                        contentLength
                    );

                    var createFileResponse = await handler.CreateFile(
                        new()
                        {
                            Headers = context.Headers,
                            HttpContext = context.HttpContext,
                            CancellationToken = context.CancellationToken,
                            Metadata = metadata,
                            ResourceLength = resourceLengthForCreate
                        }
                    );

                    if (createFileResponse.IsError)
                    {
                        return new()
                        {
                            Status = createFileResponse.Status,
                            ErrorMessage = createFileResponse.ErrorMessage
                        };
                    }

                    // New file so send the 104 response with the location header
                    await context.HttpContext.Send104UploadResumptionSupported(
                        context.HttpContext.Request.GetDisplayUrl().TrimEnd('/')
                            + "/"
                            + context.Headers.ResourceId,
                        handler.Limits
                    );
                }
                else
                {
                    await Tus2Validator.AssertFileNotCompleted(
                        storageFacade.Storage,
                        context.Headers.ResourceId
                    );
                }

                // TODO: See if we can implement some kind of updatable "request cache" for data that
                // could change during the request but were we do not wish to read the data multiple times,
                // e.g. Upload-Offset for the file retrieved from storage.

                uploadOffsetFromStorage = await storageFacade.Storage.GetOffset(
                    context.Headers.ResourceId
                );
                await Tus2Validator.AssertValidOffset(
                    uploadOffsetFromStorage.Value,
                    context.Headers.UploadOffset
                );

                var resourceLength = await storageFacade.Storage.GetResourceLength(
                    context.Headers.ResourceId
                );

                if (contentLength is not null && resourceLength is not null)
                {
                    Tus2Validator.AssertValidResourceLength(
                        resourceLength.Value,
                        context.Headers.UploadOffset.Value,
                        contentLength
                    );
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    context.HttpContext.RequestAborted,
                    ongoingCancellationToken
                );
                PipeReader guardedPipeReader = new ClientDisconnectGuardedPipeReader(
                    context.HttpContext.Request.BodyReader,
                    cts.Token
                );

                if (
                    handler.Limits is not null
                    && (
                        handler.Limits.MaxSize is not null
                        || handler.Limits.MaxAppendSize is not null
                    )
                )
                {
                    var maxSize = handler.Limits.MaxSize + (resourceLength ?? 0);
                    var maxAppendSize = handler.Limits.MaxAppendSize + (resourceLength ?? 0);

                    var sizeToUse = Math.Min(maxAppendSize ?? 0, maxSize ?? 0);

                    if (sizeToUse is not 0)
                    {
                        guardedPipeReader = new MaxReadSizeGuardedPipeReader(
                            guardedPipeReader,
                            resourceLength ?? 0,
                            sizeToUse
                        );
                    }
                }

                var writeDataContext = new WriteDataContext
                {
                    BodyReader = guardedPipeReader,
                    CancellationToken = cts.Token,
                    Headers = context.Headers,
                    HttpContext = context.HttpContext,
                    ReportOffset = handler.EnableReportingOfProgress
                        ? CreateUploadOffsetReporter(context.HttpContext)
                        : _ => Task.CompletedTask,
                    ResourceLength = resourceLength
                };

                var writeDataResponse = await handler.WriteData(writeDataContext);

                if (resourceLength is null && contentLength is not null)
                {
                    await storageFacade.Storage.SetResourceLength(
                        context.Headers.ResourceId,
                        contentLength.Value
                    );
                }

                if (ongoingCancellationToken.IsCancellationRequested)
                {
                    await uploadManager.NotifyCancelComplete(context.Headers.ResourceId);
                }

                writeDataResponse.ContentLocation = await handler.GetContentLocation(
                    context.Headers.ResourceId,
                    writeDataResponse.UploadComplete
                );
                writeDataResponse.Limits = handler.Limits;
                return writeDataResponse;
            }
            catch (Tus2AssertRequestException exc) when (exc is not Tus2ProblemDetailsException)
            {
                return new()
                {
                    Status = exc.Status,
                    ErrorMessage = exc.ErrorMessage,
                    UploadOffset = uploadOffsetFromStorage
                };
            }
        }

        private static long? AssertAndGetResourceLength(
            WriteDataContext context,
            long? contentLength
        )
        {
            Tus2Validator.AssertContentLengthAndUploadLimitAreTheSame(
                contentLength,
                context.Headers.UploadLength
            );

            long? resourceLengthForCreate = Math.Min(
                contentLength ?? long.MaxValue,
                context.Headers.UploadLength ?? long.MaxValue
            );

            return resourceLengthForCreate == long.MaxValue ? null : resourceLengthForCreate;
        }

        private static Func<long, Task> CreateUploadOffsetReporter(HttpContext httpContext)
        {
            const int SECONDS_BETWEEN_REPORTS = 1;
            var lastWrite = DateTime.MinValue;

            return async (offset) =>
            {
                if (
                    lastWrite == DateTime.MinValue
                    || (DateTime.Now - lastWrite).TotalSeconds > SECONDS_BETWEEN_REPORTS
                )
                {
                    await InformationalResponseSender.Send104UploadResumptionSupported(
                        httpContext,
                        offset
                    );
                    lastWrite = DateTime.Now;
                }
            };
        }

        private static async Task<long?> TryGetOffset(Tus2Storage storage, string uploadToken)
        {
            try
            {
                return await storage.GetOffset(uploadToken);
            }
            catch
            {
                return null;
            }
        }
    }
}
