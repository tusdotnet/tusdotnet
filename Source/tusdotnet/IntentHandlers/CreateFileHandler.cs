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
            new UploadLengthForCreateFile(),
            new UploadMetadata()
        };

        private ITusCreationStore CreationStore { get; }

        public CreateFileHandler(ContextAdapter context, ITusCreationStore creationStore) 
            : base(context, IntentType.CreateFile, LockType.NoLock)
        {
            CreationStore = creationStore;
        }

        internal override async Task<ResultType> Invoke()
        {
            var metadata = Context.Request.GetHeader(HeaderConstants.UploadMetadata);

            var response = Context.Response;
            var request = Context.Request;
            var cancellationToken = Context.CancellationToken;

            var uploadLength = request.GetUploadLength();

#warning TODO: Read header in ctor and pass to UploadMetadata so that we do not parse the header multiple times
            var parsedMetadata = Metadata.Parse(metadata);

            var onBeforeCreateResult = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = parsedMetadata;
                ctx.UploadLength = uploadLength;
            });

            if (onBeforeCreateResult == ResultType.StopExecution)
                return ResultType.StopExecution;

            var fileId = await CreationStore.CreateFileAsync(uploadLength, metadata, cancellationToken);

            await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
            {
                ctx.FileId = fileId;
                ctx.FileConcatenation = null;
                ctx.Metadata = parsedMetadata;
                ctx.UploadLength = uploadLength;
            });

            var expires = await SetExpirationIfApplicable(Context, fileId, null);

            SetReponseHeaders(Context, fileId, expires);

            response.SetStatus((int)HttpStatusCode.Created);

            return ResultType.StopExecution;
        }

        private static void SetReponseHeaders(ContextAdapter Context, string fileId, DateTimeOffset? expires)
        {
            if (expires != null)
            {
                Context.Response.SetHeader(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
            }

            Context.Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            Context.Response.SetHeader(HeaderConstants.Location, $"{Context.Configuration.UrlPath.TrimEnd('/')}/{fileId}");
        }

        private static async Task<DateTimeOffset?> SetExpirationIfApplicable(ContextAdapter Context, string fileId, DateTimeOffset? expires)
        {
            if (Context.Configuration.Store is ITusExpirationStore expirationStore
                            && Context.Configuration.Expiration != null)
            {
                expires = DateTimeOffset.UtcNow.Add(Context.Configuration.Expiration.Timeout);
                await expirationStore.SetExpirationAsync(fileId, expires.Value, Context.CancellationToken);
            }

            return expires;
        }
    }
}
