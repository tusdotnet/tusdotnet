using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
{
    internal class CreateFileHandler : IntentHandler
    {
        internal override Requirement[] Requires => new Requirement[]
        {
            new UploadLengthForCreateFileAndConcatenateFiles(),
            new UploadMetadata()
        };

        private readonly ITusCreationStore _creationStore;

        private readonly ExpirationHelper _expirationHelper;

        public CreateFileHandler(ContextAdapter context, ITusCreationStore creationStore)
            : base(context, IntentType.CreateFile, LockType.NoLock)
        {
            _creationStore = creationStore;
            _expirationHelper = new ExpirationHelper(context.Configuration);
        }

        internal override async Task Invoke()
        {
            var metadata = Request.GetHeader(HeaderConstants.UploadMetadata);
            var parsedMetadata = Metadata.Parse(metadata);


            var onBeforeCreateResult = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = parsedMetadata;
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
                ctx.Metadata = parsedMetadata;
                ctx.UploadLength = Request.UploadLength;
            });

            var expires = await _expirationHelper.SetExpirationIfSupported(fileId, CancellationToken);

            SetReponseHeaders(fileId, expires);

            Response.SetStatus(HttpStatusCode.Created);
        }

        private void SetReponseHeaders(string fileId, DateTimeOffset? expires)
        {
            if (expires != null)
            {
                Response.SetHeader(HeaderConstants.UploadExpires, _expirationHelper.FormatHeader(expires));
            }

            Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Response.SetHeader(HeaderConstants.Location, $"{Context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");
        }
    }
}