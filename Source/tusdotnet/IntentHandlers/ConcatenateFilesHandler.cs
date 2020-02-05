using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    /*
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
    * If the expiration is known at the creation, the Upload-Expires header MUST be included in the response to the initial POST request. 
     */
    internal class ConcatenateFilesHandler : IntentHandler
    {
        internal override Requirement[] Requires => BuildListOfRequirements();

        public UploadConcat UploadConcat { get; }

        private Dictionary<string, Metadata> _metadataFromRequirement;

        public ConcatenateFilesHandler(ContextAdapter context, ITusConcatenationStore concatenationStore)
            : base(context, IntentType.ConcatenateFiles, LockType.NoLock)
        {
            UploadConcat = ParseUploadConcatHeader();
            _concatenationStore = concatenationStore;
            _expirationHelper = new ExpirationHelper(context.Configuration);
        }

        private readonly ITusConcatenationStore _concatenationStore;
        private readonly ExpirationHelper _expirationHelper;

        internal override async Task Invoke()
        {
            var metadataString = Request.GetHeader(HeaderConstants.UploadMetadata);

            string fileId;
            DateTimeOffset? expires = null;

            var onBeforeCreateResult = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = _metadataFromRequirement;
                ctx.UploadLength = Request.UploadLength;
                ctx.FileConcatenation = UploadConcat.Type;
            });

            if (onBeforeCreateResult == ResultType.StopExecution)
            {
                return;
            }

            fileId = await HandleCreationOfConcatFiles(Request.UploadLength, metadataString, _metadataFromRequirement);

            if (IsPartialFile())
            {
                expires = await _expirationHelper.SetExpirationIfSupported(fileId, CancellationToken);
            }

            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.Location, $"{Context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");

            if (expires != null)
            {
                Response.SetHeader(HeaderConstants.UploadExpires, _expirationHelper.FormatHeader(expires));
            }

            Response.SetStatus(HttpStatusCode.Created);
        }

        private Requirement[] BuildListOfRequirements()
        {
            var requirements = new List<Requirement>(3)
            {
                new Validation.Requirements.UploadConcatForConcatenateFiles(UploadConcat, _concatenationStore)
            };

            // Only validate upload length for partial files as the length of a final file is implicit.
            if (IsPartialFile())
            {
                requirements.Add(new Validation.Requirements.UploadLengthForCreateFileAndConcatenateFiles());
            }

            requirements.Add(new Validation.Requirements.UploadMetadata(metadata => _metadataFromRequirement = metadata));

            return requirements.ToArray();
        }

        private bool IsPartialFile()
        {
            return UploadConcat.Type is FileConcatPartial;
        }

        private UploadConcat ParseUploadConcatHeader()
        {
            return new UploadConcat(Request.GetHeader(HeaderConstants.UploadConcat), Context.Configuration.UrlPath);
        }

        private async Task<string> HandleCreationOfConcatFiles(long uploadLength, string metadataString, Dictionary<string, Metadata> metadata)
        {
            string createdFileId;

            if (UploadConcat.Type is FileConcatFinal finalConcat)
            {
                createdFileId = await _concatenationStore.CreateFinalFileAsync(finalConcat.Files, metadataString, CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
                {
                    ctx.FileId = createdFileId;
                    ctx.Metadata = metadata;
                    ctx.UploadLength = uploadLength;
                    ctx.FileConcatenation = UploadConcat.Type;
                });

                await EventHelper.NotifyFileComplete(Context, ctx => ctx.FileId = createdFileId);
            }
            else
            {
                createdFileId = await _concatenationStore.CreatePartialFileAsync(uploadLength, metadataString, CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
                {
                    ctx.FileId = createdFileId;
                    ctx.Metadata = metadata;
                    ctx.UploadLength = uploadLength;
                    ctx.FileConcatenation = UploadConcat.Type;
                });
            }

            return createdFileId;
        }
    }
}