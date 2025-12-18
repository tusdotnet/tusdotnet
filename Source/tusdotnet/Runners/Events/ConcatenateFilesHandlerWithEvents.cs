using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Runners.Events
{
    internal class ConcatenateFilesHandlerWithEvents : IntentHandlerWithEvents
    {
        private readonly ConcatenateFilesHandler _concatenationHandler;

        public ConcatenateFilesHandlerWithEvents(IntentHandler intentHandler)
            : base(intentHandler)
        {
            _concatenationHandler = (ConcatenateFilesHandler)IntentHandler;
        }

        internal override async Task<ResultType> Authorize()
        {
            return await EventHelper.Validate<AuthorizeContext>(
                Context,
                ctx =>
                {
                    ctx.Intent = IntentType.ConcatenateFiles;
                    ctx.FileConcatenation = _concatenationHandler.UploadConcat.Type;
                }
            );
        }

        internal override async Task NotifyAfterAction()
        {
            if (_concatenationHandler.UploadConcat.Type is FileConcatFinal fileConcatFinal)
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
                        ctx.FileConcatenation = _concatenationHandler.UploadConcat.Type;
                        ctx.Context = Context;
                    }
                );
            }
        }

        internal override async Task<ResultType> ValidateBeforeAction()
        {
            return await EventHelper.Validate<BeforeCreateContext>(
                Context,
                ctx =>
                {
                    ctx.Metadata = Context.ParsedRequest.Metadata;
                    ctx.UploadLength = Context.Request.Headers.UploadLength;
                    ctx.FileConcatenation = _concatenationHandler.UploadConcat.Type;
                }
            );
        }
    }
}
