using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Runners.Events
{
    internal class WriteFileHandlerWithEvents : IntentHandlerWithEvents
    {
        public WriteFileHandlerWithEvents(IntentHandler intentHandler)
            : base(intentHandler) { }

        internal override async Task<ResultType> Authorize()
        {
            return await EventHelper.Validate<AuthorizeContext>(
                Context,
                ctx =>
                {
                    ctx.Intent = IntentType.WriteFile;
                    ctx.FileConcatenation = Context.Cache?.UploadConcat?.Type;
                }
            );
        }

        internal override async Task NotifyAfterAction()
        {
            var offset =
                Context.Cache.UploadOffset
                ?? await Context.StoreAdapter.GetUploadOffsetAsync(
                    Context.FileId,
                    CancellationToken.None
                );
            var length = await Context.StoreAdapter.GetUploadLengthAsync(
                Context.FileId,
                CancellationToken.None
            );

            if (offset != length)
                return;

            if (Context.StoreAdapter.Extensions.Concatenation)
            {
                var fileConcat = await Context.StoreAdapter.GetUploadConcatAsync(
                    Context.FileId,
                    CancellationToken.None
                );
                if (fileConcat is FileConcatPartial)
                    return;
            }

            await EventHelper.NotifyFileComplete(Context);
        }

        internal override async Task<ResultType> ValidateBeforeAction()
        {
            return await EventHelper.Validate<BeforeWriteContext>(
                Context,
                ctx =>
                {
                    ctx.UploadOffset = Context.Request.Headers.UploadOffset;
                }
            );
        }
    }
}
