using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
{
    /*
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
    * If the expiration is known at the creation, the Upload-Expires header MUST be included in the response to the initial POST request. 
    * 
    * The Client MAY include parts of the upload in the initial Creation request using the Creation With Upload extension.
    */
    internal class CreateFileHandler : IntentHandler
    {
        internal override Requirement[] Requires => new Requirement[]
        {
            new UploadLengthForCreateFileAndConcatenateFiles(),
            new UploadMetadata(metadata => _metadataFromRequirement = metadata)
        };

        private readonly ITusCreationStore _creationStore;

        private readonly ExpirationHelper _expirationHelper;

        private Dictionary<string, Metadata> _metadataFromRequirement;

        public CreateFileHandler(ContextAdapter context, ITusCreationStore creationStore)
            : base(context, IntentType.CreateFile, LockType.NoLock)
        {
            _creationStore = creationStore;
            _expirationHelper = new ExpirationHelper(context.Configuration);
        }

        internal override async Task Invoke()
        {
            var metadata = Request.GetHeader(HeaderConstants.UploadMetadata);

            var onBeforeCreateResult = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = _metadataFromRequirement;
                ctx.UploadLength = Request.UploadLength;
            });

            if (onBeforeCreateResult == ResultType.StopExecution)
            {
                return;
            }

            var fileId = await _creationStore.CreateFileAsync(Request.UploadLength, metadata, CancellationToken);

            await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
            {
                ctx.FileId = fileId;
                ctx.FileConcatenation = null;
                ctx.Metadata = _metadataFromRequirement;
                ctx.UploadLength = Request.UploadLength;
            });

            var expires = await _expirationHelper.SetExpirationIfSupported(fileId, CancellationToken);

            long? uploadOffset = null;
            var writeFileContext = await WriteFileContextForCreationWithUpload.FromCreationContext(Context, fileId);
            if (writeFileContext.FileContentIsAvailable)
            {
                uploadOffset = await writeFileContext.SaveFileContent();
                expires = writeFileContext.UploadExpires;
            }

            SetReponseHeaders(fileId, expires, uploadOffset);

            Response.SetStatus(HttpStatusCode.Created);
        }

        private void SetReponseHeaders(string fileId, DateTimeOffset? expires, long? uploadOffset)
        {
            if (expires != null)
            {
                Response.SetHeader(HeaderConstants.UploadExpires, _expirationHelper.FormatHeader(expires));
            }

            if (uploadOffset != null)
            {
                Response.SetHeader(HeaderConstants.UploadOffset, uploadOffset.Value.ToString());
            }

            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.Location, $"{Context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");
        }
    }
}