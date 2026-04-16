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

        internal override Task<ResultType> Authorize()
        {
            return EventHelper.Validate<AuthorizeContext>(
                Context,
                ctx =>
                {
                    ctx.Intent = IntentType.DeleteFile;
                }
            );
        }

        internal override Task NotifyAfterAction()
        {
            return EventHelper.Notify<DeleteCompleteContext>(Context);
        }

        internal override Task<ResultType> ValidateBeforeAction()
        {
            return EventHelper.Validate<BeforeDeleteContext>(Context);
        }
    }
}
