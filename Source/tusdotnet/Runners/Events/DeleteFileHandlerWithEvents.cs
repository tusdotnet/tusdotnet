using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Runners.Events
{
    internal class DeleteFileHandlerWithEvents : IntentHandlerWithEvents<DeleteFileHandler>
    {
        public DeleteFileHandlerWithEvents(DeleteFileHandler intentHandler)
            : base(intentHandler) { }

        internal override async Task<ResultType> Authorize()
        {
            return await EventHelper.Validate<AuthorizeContext>(
                Context,
                ctx =>
                {
                    ctx.Intent = IntentType.DeleteFile;
                }
            );
        }

        internal override async Task NotifyAfterAction()
        {
            await EventHelper.Notify<DeleteCompleteContext>(Context);
        }

        internal override async Task<ResultType> ValidateBeforeAction()
        {
            return await EventHelper.Validate<BeforeDeleteContext>(Context);
        }
    }
}
