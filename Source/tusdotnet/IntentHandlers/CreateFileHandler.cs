using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Validation;
using tusdotnet.Validation.Requirements;

namespace tusdotnet.IntentHandlers
{
    internal class CreateFileHandler : IntentHandler
    {
        internal override bool RequiresLock => false;

        internal override Requirement[] Requires => new Requirement[]
        {
            new UploadLengthForCreateFile(),
            new UploadMetadata()
        };

        private ITusCreationStore CreationStore { get; }

        internal override IntentType Intent => IntentType.CreateFile;

        public CreateFileHandler(ContextAdapter context, ITusCreationStore creationStore) : base(context)
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

#warning Change to something like Context.Raise<BeforeCreateContext>(ctx => /* configure */ );
            if (await HandleOnBeforeCreateAsync(Context, metadata, uploadLength))
            {
                return ResultType.Handled;
            }

            var fileId = await CreationStore.CreateFileAsync(uploadLength, metadata, cancellationToken);

            await HandleOnCreateComplete(Context, fileId, metadata, uploadLength);

            var expires = await SetExpirationIfApplicable(Context, fileId, null);

            SetReponseHeaders(Context, fileId, expires);

            response.SetStatus((int)HttpStatusCode.Created);

            return ResultType.Handled;
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

        private static Task<bool> HandleOnBeforeCreateAsync(ContextAdapter Context, string metadata, long uploadLength)
        {
            if (Context.Configuration.Events?.OnBeforeCreateAsync == null)
            {
                return Task.FromResult(false);
            }

            return HandleOnBeforeCreateAsyncLocal();

            async Task<bool> HandleOnBeforeCreateAsyncLocal()
            {
                var beforeCreateContext = BeforeCreateContext.Create(Context, ctx =>
                {
                    ctx.FileConcatenation = null;
                    ctx.Metadata = Metadata.Parse(metadata);
                    ctx.UploadLength = uploadLength;
                });

                await Context.Configuration.Events.OnBeforeCreateAsync(beforeCreateContext);

                if (beforeCreateContext.HasFailed)
                {
                    return await Context.Response.Error(HttpStatusCode.BadRequest, beforeCreateContext.ErrorMessage);
                }

                return false;
            }
        }

        private static Task HandleOnCreateComplete(ContextAdapter Context, string fileId, string metadata, long uploadLength)
        {
            if (Context.Configuration.Events?.OnCreateCompleteAsync == null)
            {
                return Task.FromResult(0);
            }

            return Context.Configuration.Events.OnCreateCompleteAsync(CreateCompleteContext.Create(Context, ctx =>
            {
                ctx.FileId = fileId;
                ctx.FileConcatenation = null;
                ctx.Metadata = Metadata.Parse(metadata);
                ctx.UploadLength = uploadLength;
            }));
        }
    }
}
