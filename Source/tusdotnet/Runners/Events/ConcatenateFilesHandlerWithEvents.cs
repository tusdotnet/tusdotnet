using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Runners.Events
{
    internal class ConcatenateFilesHandlerWithEvents
        : IntentHandlerWithEvents<ConcatenateFilesHandler>
    {
        public ConcatenateFilesHandlerWithEvents(ConcatenateFilesHandler intentHandler)
            : base(intentHandler) { }

        internal override Task<ResultType> Authorize()
        {
            return EventHelper.Validate<AuthorizeContext>(
                Context,
                ctx =>
                {
                    ctx.Intent = IntentType.ConcatenateFiles;
                    ctx.FileConcatenation = TypedIntentHandler.UploadConcat.Type;
                }
            );
        }

        internal override async Task NotifyAfterAction()
        {
            if (TypedIntentHandler.UploadConcat.Type is FileConcatFinal fileConcatFinal)
            {
                await EventHelper.Notify<CreateCompleteContext>(
                    Context,
                    ctx =>
                    {
                        ctx.FileId = Context.FileId;
                        ctx.Metadata = Context.ParsedRequest.Metadata;
                        ctx.UploadLength = Context.Request.Headers.UploadLength;
                        ctx.FileConcatenation = fileConcatFinal;
                        ctx.Context = Context;
                    }
                );

                await EventHelper.NotifyFileComplete(Context, ctx => ctx.FileId = Context.FileId);
            }
            else
            {
                await EventHelper.Notify<CreateCompleteContext>(
                    Context,
                    ctx =>
                    {
                        ctx.FileId = Context.FileId;
                        ctx.Metadata = Context.ParsedRequest.Metadata;
                        ctx.UploadLength = Context.Request.Headers.UploadLength;
                        ctx.FileConcatenation = TypedIntentHandler.UploadConcat.Type;
                        ctx.Context = Context;
                    }
                );
            }
        }

        internal override Task<ResultType> ValidateBeforeAction()
        {
            return EventHelper.Validate<BeforeCreateContext>(
                Context,
                ctx =>
                {
                    ctx.Metadata = Context.ParsedRequest.Metadata;
                    ctx.UploadLength = Context.Request.Headers.UploadLength;
                    ctx.FileConcatenation = TypedIntentHandler.UploadConcat.Type;
                }
            );
        }
    }
}
