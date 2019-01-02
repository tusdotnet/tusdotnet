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

        public ConcatenateFilesHandler(ContextAdapter context)
            : base(context, IntentType.ConcatenateFiles, LockType.NoLock)
        {
            _uploadConcat = ParseUploadConcatHeader(context);
            _concatenationStore = (ITusConcatenationStore)context.Configuration.Store;
        }

        internal override Requirement[] Requires => new Requirement[]
        {
            new Validation.Requirements.UploadConcatForConcatenateFiles(_uploadConcat, _concatenationStore),
            new Validation.Requirements.UploadLength(),
            new Validation.Requirements.UploadMetadata()
        };

        internal override async Task<ResultType> Invoke()
        {
            var metadataString = Context.Request.GetHeader(HeaderConstants.UploadMetadata);
            var metadata = Metadata.Parse(metadataString);

            string fileId;
            DateTimeOffset? expires = null;

            var response = Context.Response;
            var request = Context.Request;
            var cancellationToken = Context.CancellationToken;

            var uploadLength = GetUploadLength(Context.Request);

            var onBeforeCreateResult = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = metadata;
                ctx.UploadLength = uploadLength;
                ctx.FileConcatenation = _uploadConcat.Type;
            });

            if (onBeforeCreateResult == ResultType.StopExecution)
                return ResultType.StopExecution;

            fileId = await HandleCreationOfConcatFiles(Context, uploadLength, metadataString, metadata);

#warning TODO: Implement better handling of store interfaces (expiration service?)

            // context.Supports<ITusExpirationStore>(); context.StoreAs<ITusExpirationStore>()
            if (Context.Configuration.Store is ITusExpirationStore expirationStore
                && Context.Configuration.Expiration != null
                && (IsPartialFile()))
            {
#warning TODO: Replace with SystemTimeOffset
                expires = DateTimeOffset.UtcNow.Add(Context.Configuration.Expiration.Timeout);
                await expirationStore.SetExpirationAsync(fileId, expires.Value, Context.CancellationToken);
            }

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.Location, $"{Context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");

            if (expires != null)
            {
                response.SetHeader(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
            }

            response.SetStatus((int)HttpStatusCode.Created);
            return ResultType.StopExecution;
        }

        private bool IsPartialFile()
        {
            return _uploadConcat.Type is FileConcatPartial;
        }

        private UploadConcat ParseUploadConcatHeader(ContextAdapter context)
        {
            return new UploadConcat(context.Request.GetHeader(HeaderConstants.UploadConcat), context.Configuration.UrlPath);
        }

        private static long GetUploadLength(RequestAdapter request)
        {
            return request.Headers.ContainsKey(HeaderConstants.UploadDeferLength)
                ? -1
                : long.Parse(request.GetHeader(HeaderConstants.UploadLength) ?? "-1");
        }

        private async Task<string> HandleCreationOfConcatFiles(
          ContextAdapter context,
          long uploadLength,
          string metadataString,
          Dictionary<string, Metadata> metadata)
        {
            string createdFileId;
            if (_uploadConcat.Type is FileConcatPartial)
            {
                createdFileId = await _concatenationStore.CreatePartialFileAsync(uploadLength, metadataString, context.CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(context, ctx =>
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
                createdFileId = await _concatenationStore.CreateFinalFileAsync(finalConcat.Files, metadataString, context.CancellationToken);

                await EventHelper.Notify<CreateCompleteContext>(context, ctx =>
                {
                    ctx.FileId = createdFileId;
                    ctx.Metadata = metadata;
                    ctx.UploadLength = uploadLength;
                    ctx.FileConcatenation = _uploadConcat.Type;
                });

                await EventHelper.NotifyFileComplete(context, ctx => ctx.FileId = createdFileId);
            }

            return createdFileId;
        }
    }
}