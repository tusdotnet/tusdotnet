using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Runners.Events
{
    internal class CreateFileWithEvents : IntentHandlerWithEvents
    {
        public CreateFileWithEvents(IntentHandler handler) : base(handler)
        {
        }

        internal override async Task<ResultType> Authorize()
        {
            return await EventHelper.Validate<AuthorizeContext>(Context, ctx =>
            {
                ctx.Intent = IntentType.CreateFile;
            });
        }

        internal override async Task NotifyAfterAction()
        {
            await EventHelper.Notify<CreateCompleteContext>(Context, ctx =>
            {
                ctx.FileId = Context.FileId;
                ctx.FileConcatenation = null;
                ctx.Metadata = Context.Cache.Metadata;
                ctx.UploadLength = Context.Request.Headers.UploadLength;
                ctx.Context = Context;
            });

            var isEmptyFile = Context.Request.Headers.UploadLength == 0;

            if (isEmptyFile)
            {
                // Normally we would call NotifyFileComplete from WriteFileHandler but since we never use 
                // WriteFileContextForCreationWithUpload if the file is empty, nor allow PATCH requests for the file, we need to trigger the event here. 
                await EventHelper.NotifyFileComplete(Context);
            }
        }

        internal override async Task<ResultType> ValidateBeforeAction()
        {
            var result = await EventHelper.Validate<BeforeCreateContext>(Context, ctx =>
            {
                ctx.Metadata = Context.Cache.Metadata;
                ctx.UploadLength = Context.Request.Headers.UploadLength;
            });

            return result;
        }
    }
}
