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
        private readonly UploadConcat _uploadConcat;

        private readonly ITusConcatenationStore _concatenationStore;

        private readonly ExpirationHelper _expirationHelper;

        public ConcatenateFilesHandler(ContextAdapter context, ITusConcatenationStore concatenationStore)
            : base(context, IntentType.ConcatenateFiles, LockType.NoLock)
        {
            _uploadConcat = ParseUploadConcatHeader(context);
            _concatenationStore = concatenationStore;
            _expirationHelper = new ExpirationHelper(context.Configuration);
        }

        internal override Requirement[] Requires => BuildListOfRequirements();

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
                ctx.FileConcatenation = _uploadConcat.Type;
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

        private bool IsPartialFile()
        {
            return _uploadConcat.Type is FileConcatPartial;
        }

        private Requirement[] BuildListOfRequirements()
        {
            var requirements = new List<Requirement>(3)
            {
                new Validation.Requirements.UploadConcatForConcatenateFiles(_uploadConcat, _concatenationStore)
            };

            // Only validate upload length for partial files as the length of a final file is implicit.
            if (IsPartialFile())
            {
                requirements.Add(new Validation.Requirements.UploadLengthForCreateFileAndConcatenateFiles());
            }

            requirements.Add(new Validation.Requirements.UploadMetadata());

            return requirements.ToArray();
        }

        private UploadConcat ParseUploadConcatHeader(ContextAdapter context)
        {
            return new UploadConcat(Request.GetHeader(HeaderConstants.UploadConcat), context.Configuration.UrlPath);
        }

        private async Task<string> HandleCreationOfConcatFiles(long uploadLength, string metadataString, Dictionary<string, Metadata> metadata)
        {
            string createdFileId;
            if (_uploadConcat.Type is FileConcatPartial)
            {
                createdFileId = await _concatenationStore.CreatePartialFileAsync(uploadLength, metadataString, CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
                {
                    ctx.FileId = createdFileId;
                    ctx.Metadata = metadata;
                    ctx.UploadLength = uploadLength;
                    ctx.FileConcatenation = _uploadConcat.Type;
                });
            }
            else
            {
                var finalConcat = (FileConcatFinal)_uploadConcat.Type;
                createdFileId = await _concatenationStore.CreateFinalFileAsync(finalConcat.Files, metadataString, CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
                {
                    ctx.FileId = createdFileId;
                    ctx.Metadata = metadata;
                    ctx.UploadLength = uploadLength;
                    ctx.FileConcatenation = _uploadConcat.Type;
                });

                await EventHelper.NotifyFileComplete(Context, ctx => ctx.FileId = createdFileId);
            }

            return createdFileId;
        }
    }
}