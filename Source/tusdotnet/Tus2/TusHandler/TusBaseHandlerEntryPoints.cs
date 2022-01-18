using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    public abstract class TusBaseHandlerEntryPoints
    {
        public ITus2Storage Store{ get; set; }

        public IMetadataParser MetadataParser { get; set; }

        public bool AllowClientToDeleteFile { get; set; }

        public Tus2Headers Headers { get; set; }
        
        public HttpContext HttpContext { get; set; }

        internal IOngoingUploadManager UploadManager { get; set; }

        public virtual bool IsAllowedToDeleteFile { get; }

        internal async Task<UploadRetrievingProcedureResponse> RetrieveOffsetEntryPoint()
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

            try
            {
                Tus2Validator.AssertNoInvalidHeaders(Headers);

                await UploadManager.CancelOtherUploads(Headers.UploadToken);

                await Tus2Validator.AssertFileExist(Store, Headers.UploadToken);

                return await OnRetrieveOffset();

            }
            catch (Tus2AssertRequestException ex)
            {
                return new()
                {
                    Status = ex.Status,
                    ErrorMessage = ex.ErrorMessage
                };
            }
        }
        internal async Task<UploadCancellationProcedureResponse> DeleteEntryPoint()
        {
            /*
            If the client wants to terminate the transfer without the ability to resume, it MAY send a DELETE request to the server along with the Upload-Token which is an indication that the client is no longer interested in uploading this body and the server can release resources associated with this token. The client MUST NOT initiate this procedure without the knowledge of server support.

            The request MUST use the DELETE method and include the Upload-Token header. The request MUST NOT include the Upload-Offset header or the Upload-Incomplete header. The server MUST reject the request with the Upload-Offset header or the Upload-Incomplete header by sending a 400 (Bad Request) response.

            If the server has successfully released the resources allocated for this token, it MUST send back a 204 (No Content) response.

            The server MAY terminate any ongoing Upload Transfer Procedure ({{upload-transfer}}) for the same token before sending the response by abruptly terminating the HTTP connection or stream.

            If the server has no record of the token in Upload-Token, it MUST respond with 404 (Not Found) status code.

            If the server does not support cancellation, it MUST respond with 405 (Method Not Allowed) status code.
             * */
            try
            {
                if (!AllowClientToDeleteFile)
                {
                    return new UploadCancellationProcedureResponse
                    {
                        Status = HttpStatusCode.MethodNotAllowed
                    };
                }

                Tus2Validator.AssertNoInvalidHeaders(Headers);

                await UploadManager.CancelOtherUploads(Headers.UploadToken);

                await Tus2Validator.AssertFileExist(Store, Headers.UploadToken);

                return await OnDelete();

            }
            catch (Tus2AssertRequestException ex)
            {
                return new()
                {
                    Status = ex.Status,
                    ErrorMessage = ex.ErrorMessage
                };
            }
        }

        internal async Task<UploadTransferProcedureResponse> WriteDataEntryPoint()
        {
            /*
            The Upload Transfer Procedure is intended for transferring the data chunk. As such, it can be used for either resuming an existing upload, or starting a new upload. A limited form of this procedure MAY be used by the client to start a new upload without the knowledge of server support.

            This procedure is designed to be compatible with a regular upload. Therefore all methods are allowed with the exception of GET, HEAD, DELETE, and OPTIONS. All response status codes are allowed. The client is RECOMMENDED to use the POST method if not otherwise intended. The server MAY only support a limited number of methods.

            The client MUST use the same method throughout an entire upload. The server SHOULD reject the attempt to resume an upload with a different method with 400 (Bad Request) response.

            The request MUST include the Upload-Token header field ({{upload-token}}) which uniquely identifies an upload. The client MUST NOT reuse the token for a different upload.

            When resuming an upload, the Upload-Offset header field ({{upload-offset}}) MUST be set to the resumption offset. The resumption offset 0 indicates a new upload. The absence of the Upload-Offset header field implies the resumption offset of 0.

            If the end of the request body is not the end of the upload, the Upload-Incomplete header field ({{upload-incomplete}}) MUST be set to true.

            The client MAY send the metadata of the file using headers such as Content-Type (see {{Section 8.3 of HTTP}} and Content-Disposition {{!RFC6266}} when starting a new upload. It is OPTIONAL for the client to repeat the metadata when resuming an upload.

            If the server does not consider the upload associated with the token in the Upload-Token header field active, but the resumption offset is non-zero, it MUST respond with 404 (Not Found) status code.

            The client MUST NOT perform multiple Upload Transfer Procedures ({{upload-transfer}}) for the same token in parallel to avoid race conditions and data loss or corruption. The server is RECOMMENDED to take measures to avoid parallel Upload Transfer Procedures: The server MAY terminate any ongoing Upload Transfer Procedure ({{upload-transfer}}) for the same token. Since the client is not allowed to perform multiple transfers in parallel, the server can assume that the previous attempt has already failed. Therefore, the server MAY abruptly terminate the previous HTTP connection or stream.

            If the offset in the Upload-Offset header field does not match the value 0, the offset provided by the immediate previous Offset Retrieving Procedure ({{offset-retrieving}}), or the end offset of the immediate previous incomplete transfer, the server MUST respond with 409 (Conflict) status code.

            If the request completes successfully and the entire upload is complete, the server MUST acknowledge it by responding with a successful status code between 200 and 299 (inclusive). Server is RECOMMENDED to use 201 (Created) response if not otherwise specified. The response MUST NOT include the Upload-Incomplete header with the value of true.

            If the request completes successfully but the entire upload is not yet complete indicated by the Upload-Incomplete header, the server MUST acknowledge it by responding with the 201 (Created) status code and the Upload-Incomplete header set to true.
              * */

            try
            {
                await UploadManager.CancelOtherUploads(Headers.UploadToken);

                var ongoingCancellationToken = await UploadManager.StartUpload(Headers.UploadToken);
                await using var finishOngoing = Deferrer.Defer(() => UploadManager.FinishUpload(Headers.UploadToken));

                var metadata = MetadataParser?.Parse(HttpContext);

                Headers.UploadOffset ??= 0;

                var fileExist = await Tus2Validator.AssertFileExist(Store, Headers.UploadToken, Headers.UploadOffset != 0);

                if (!fileExist)
                {
                    var createFileResponse = await OnCreateFile(new() { Metadata = metadata });
                    if (createFileResponse.IsError)
                    {
                        return new()
                        {
                            Status = createFileResponse.Status,
                            ErrorMessage = createFileResponse.ErrorMessage
                        };
                    }
                }
                else
                {
                    var fileIsComplete = await Store.IsComplete(Headers.UploadToken);
                    if (fileIsComplete)
                    {
                        return new()
                        {
                            Status = HttpStatusCode.BadRequest,
                            ErrorMessage = "File is already completed"
                        };
                    }
                }

                // TODO: "before processing the request body" seems a bit strange here
                // "The server MUST terminate any ongoing Upload Transfer Procedure for the same token before processing the request body."
                // as we will end up with mismatches in upload-offset?
                // Should we cancel ongoing uploads directly or just before processing the body?

                await Tus2Validator.AssertValidOffset(Store, Headers.UploadToken, Headers.UploadOffset);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted, ongoingCancellationToken);
                var guardedPipeReader = new ClientDisconnectGuardedPipeReader(HttpContext.Request.BodyReader, cts.Token);

                var writeDataContext = new WriteDataContext()
                {
                    BodyReader = guardedPipeReader,
                    CancellationToken = cts.Token,
                    Metadata = metadata
                };

                var writeDataResponse = await OnWriteData(writeDataContext);

                if (ongoingCancellationToken.IsCancellationRequested)
                {
                    await UploadManager.NotifyCancelComplete(Headers.UploadToken);
                }

                return writeDataResponse;

            }
            catch (Tus2AssertRequestException exc)
            {
                return new()
                {
                    Status = exc.Status,
                    ErrorMessage = exc.ErrorMessage
                };
            }
        }

        public abstract Task<UploadRetrievingProcedureResponse> OnRetrieveOffset();

        public abstract Task<CreateFileProcedureResponse> OnCreateFile(CreateFileContext createFileContext);

        public abstract Task<UploadTransferProcedureResponse> OnWriteData(WriteDataContext writeDataContext);

        public abstract Task<UploadCancellationProcedureResponse> OnDelete();

        public abstract Task OnFileComplete();
    }
}
