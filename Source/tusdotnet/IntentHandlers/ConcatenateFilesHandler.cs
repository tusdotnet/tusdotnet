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
    internal class ConcatenateFilesHandler : IntentHandler
    {
        internal override Requirement[] Requires => BuildListOfRequirements();

        public UploadConcat UploadConcat { get; private set; }

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
            var metadata = Metadata.Parse(metadataString);

            string fileId;
            DateTimeOffset? expires = null;

            var onBeforeCreateResult = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = metadata;
                ctx.UploadLength = Request.UploadLength;
                ctx.FileConcatenation = UploadConcat.Type;
            });

            if (onBeforeCreateResult == ResultType.StopExecution)
            {
                return;
            }

            fileId = await HandleCreationOfConcatFiles(Request.UploadLength, metadataString, metadata);

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

            requirements.Add(new Validation.Requirements.UploadMetadata());

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