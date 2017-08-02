using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models.Concatenation;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.ProtocolHandlers
{
    /*
    * Creation:
    * The Client MUST send a POST request against a known upload creation URL to request a new upload resource. 
    * The request MUST include one of the following headers:
    * a) Upload-Length to indicate the size of an entire upload in bytes.
    * b) Upload-Defer-Length: 1 if upload size is not known at the time. 
    * Once it is known the Client MUST set the Upload-Length header in the next PATCH request. 
    * Once set the length MUST NOT be changed. As long as the length of the upload is not known, t
    * he Server MUST set Upload-Defer-Length: 1 in all responses to HEAD requests.
    * If the Server supports deferring length, it MUST add creation-defer-length to the Tus-Extension header.
    * The Client MAY supply the Upload-Metadata header to add additional metadata to the upload creation request. 
    * The Server MAY decide to ignore or use this information to further process the request or to reject it. 
    * If an upload contains additional metadata, responses to HEAD requests MUST include the Upload-Metadata 
    * header and its value as specified by the Client during the creation.
    * If the length of the upload exceeds the maximum, which MAY be specified using the Tus-Max-Size header, 
    * the Server MUST respond with the 413 Request Entity Too Large status.
    * The Server MUST acknowledge a successful upload creation with the 201 Created status. 
    * The Server MUST set the Location header to the URL of the created resource. This URL MAY be absolute or relative.
    * The Client MUST perform the actual upload using the core protocol.
    * 
    * Concatenation:
    * This extension can be used to concatenate multiple uploads into a single one enabling Clients to perform parallel uploads and 
    * to upload non-contiguous chunks. If the Server supports this extension, it MUST add concatenation to the Tus-Extension header.
    * A partial upload represents a chunk of a file. It is constructed by including the Upload-Concat: partial header 
    * while creating a new upload using the Creation extension. Multiple partial uploads are concatenated into a 
    * final upload in the specified order. The Server SHOULD NOT process these partial uploads until they are 
    * concatenated to form a final upload. The length of the final upload MUST be the sum of the length of all partial uploads.
    * In order to create a new final upload the Client MUST add the Upload-Concat header to the upload creation request. 
    * The value MUST be final followed by a semicolon and a space-separated list of the partial upload URLs that need to be concatenated. 
    * The partial uploads MUST be concatenated as per the order specified in the list. 
    * This concatenation request SHOULD happen after all of the corresponding partial uploads are completed.
    * The Client MUST NOT include the Upload-Length header in the final upload creation.
    * The Client MAY send the concatenation request while the partial uploads are still in progress.
    * This feature MUST be explicitly announced by the Server by adding concatenation-unfinished to the Tus-Extension header.
    * When creating a new final upload the partial uploads’ metadata SHALL NOT be transferred to the new final upload.
    * All metadata SHOULD be included in the concatenation request using the Upload-Metadata header.
    * The Server MAY delete partial uploads after concatenation. They MAY however be used multiple times to form a final resource.
    * 
    * Expiration:
    * If the expiration is known at the creation, the Upload-Expires header MUST be included in the response to the initial POST request. 
    */
    internal class PostHandler : ProtocolMethodHandler
    {
        internal override bool RequiresLock => false;

        internal override Requirement[] Requires => new Requirement[]
        {
            new Validation.Requirements.UploadConcat(),
            new UploadLength(),
            new UploadMetadata()
        };

        internal override bool CanHandleRequest(ContextAdapter context)
        {
            return context.Configuration.Store is ITusCreationStore && context.UrlMatchesUrlPath();
        }

        internal override async Task<bool> Handle(ContextAdapter context)
        {
            var metadata = context.Request.GetHeader(HeaderConstants.UploadMetadata);

            string fileId;
            DateTimeOffset? expires = null;

            var response = context.Response;
            var request = context.Request;
            var cancellationToken = context.CancellationToken;

            var tusConcatenationStore = context.Configuration.Store as ITusConcatenationStore;
            var tusCreationStore = (ITusCreationStore) context.Configuration.Store;

            var uploadConcat = request.Headers.ContainsKey(HeaderConstants.UploadConcat)
                ? new Models.Concatenation.UploadConcat(request.Headers[HeaderConstants.UploadConcat].First(),
                    context.Configuration.UrlPath)
                : null;

            var uploadLength = GetUploadLength(context.Request);

            if (tusConcatenationStore != null && uploadConcat != null)
            {
                fileId = await HandleCreationOfConcatFiles(context, uploadConcat, tusConcatenationStore, uploadLength, metadata, cancellationToken);
            }
            else
            {
                fileId = await tusCreationStore.CreateFileAsync(uploadLength, metadata, cancellationToken);
            }

            if (context.Configuration.Store is ITusExpirationStore expirationStore
                && context.Configuration.Expiration != null
                && !(uploadConcat?.Type is FileConcatFinal))
            {
                expires = DateTimeOffset.UtcNow.Add(context.Configuration.Expiration.Timeout);
                await expirationStore.SetExpirationAsync(fileId, expires.Value, context.CancellationToken);
            }

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.Location, $"{context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");

            if (expires != null)
            {
                response.SetHeader(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
            }

            response.SetStatus((int)HttpStatusCode.Created);
            return true;
        }

        private static async Task<string> HandleCreationOfConcatFiles(ContextAdapter context, Models.Concatenation.UploadConcat uploadConcat,
            ITusConcatenationStore tusConcatenationStore, long uploadLength, string metadata,
            CancellationToken cancellationToken)
        {
            string fileId;
            if (uploadConcat.Type is FileConcatPartial)
            {
                fileId = await tusConcatenationStore
                    .CreatePartialFileAsync(uploadLength, metadata, cancellationToken);
            }
            else
            {
                var finalConcat = (FileConcatFinal) uploadConcat.Type;
                fileId = await tusConcatenationStore.CreateFinalFileAsync(finalConcat.Files, metadata, cancellationToken);

                // Run callback that the final file is completed.
                if (context.Configuration.OnUploadCompleteAsync != null)
                {
                    await context.Configuration.OnUploadCompleteAsync(fileId, context.Configuration.Store, cancellationToken);
                }
            }

            return fileId;
        }

        private static long GetUploadLength(RequestAdapter request)
        {
            return request.Headers.ContainsKey(HeaderConstants.UploadDeferLength)
                ? -1
                : long.Parse(request.GetHeader(HeaderConstants.UploadLength) ?? "-1");
        }
    }
}