using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    internal class ConcatenateFilesHandler : IntentHandler
    {
        internal override bool RequiresLock => false;

        internal override IntentType Intent => IntentType.ConcatenateFiles;

        private readonly UploadConcat _uploadConcat;

        private readonly ITusConcatenationStore _concatenationStore;

        public ConcatenateFilesHandler(ContextAdapter context) : base(context)
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

            if (await HandleOnBeforeCreateAsync(Context, _uploadConcat, metadata, uploadLength))
            {
                return ResultType.Handled;
            }

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
            return ResultType.Handled;
        }

        private bool IsPartialFile()
        {
            return _uploadConcat.Type is FileConcatPartial;
        }

        private UploadConcat ParseUploadConcatHeader(ContextAdapter context)
        {
            return new UploadConcat(context.Request.GetHeader(HeaderConstants.UploadConcat), context.Configuration.UrlPath);
        }

        private static async Task<bool> HandleOnBeforeCreateAsync(ContextAdapter context, UploadConcat uploadConcat, Dictionary<string, Metadata> metadata, long uploadLength)
        {
            if (context.Configuration.Events?.OnBeforeCreateAsync == null)
            {
                return false;
            }

            var beforeCreateContext = BeforeCreateContext.Create(context, ctx =>
            {
                ctx.Metadata = metadata;
                ctx.UploadLength = uploadLength;
                ctx.FileConcatenation = uploadConcat.Type;
            });

            await context.Configuration.Events.OnBeforeCreateAsync(beforeCreateContext);

            if (beforeCreateContext.HasFailed)
            {
                return await context.Response.Error(HttpStatusCode.BadRequest, beforeCreateContext.ErrorMessage);
            }

            return false;
        }

        private static Task HandleOnCreateComplete(
        ContextAdapter context, string fileId, UploadConcat uploadConcat, Dictionary<string, Metadata> metadata, long uploadLength)
        {
            if (context.Configuration.Events?.OnCreateCompleteAsync == null)
            {
                return Task.FromResult(0);
            }

            return context.Configuration.Events.OnCreateCompleteAsync(CreateCompleteContext.Create(context, ctx =>
            {
                ctx.FileId = fileId;
                ctx.Metadata = metadata;
                ctx.UploadLength = uploadLength;
                ctx.FileConcatenation = uploadConcat.Type;
            }));
        }

        private static async Task HandleOnFileComplete(ContextAdapter context, string createdFileId)
        {
            if (context.Configuration.OnUploadCompleteAsync != null)
            {
                await context.Configuration.OnUploadCompleteAsync(createdFileId, context.Configuration.Store,
                    context.CancellationToken);
            }

            if (context.Configuration.Events?.OnFileCompleteAsync != null)
            {
                await context.Configuration.Events.OnFileCompleteAsync(FileCompleteContext.Create(context, ctx => ctx.FileId = createdFileId));
            }
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

                await HandleOnCreateComplete(context, createdFileId, _uploadConcat, metadata, uploadLength);
            }
            else
            {
                var finalConcat = (FileConcatFinal)_uploadConcat.Type;
                createdFileId = await _concatenationStore.CreateFinalFileAsync(finalConcat.Files, metadataString, context.CancellationToken);

                await HandleOnCreateComplete(context, createdFileId, _uploadConcat, metadata, uploadLength);

                // Run callback that the final file is completed.
                await HandleOnFileComplete(context, createdFileId);
            }

            return createdFileId;
        }
    }
}