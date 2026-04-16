using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Runners.Events
{
    internal class GetOptionsHandlerWithEvents : IntentHandlerWithEvents<GetOptionsHandler>
    {
        public GetOptionsHandlerWithEvents(GetOptionsHandler intentHandler)
            : base(intentHandler) { }

        internal override Task<ResultType> Authorize()
        {
            return EventHelper.Validate<AuthorizeContext>(
                Context,
                ctx =>
                {
                    ctx.Intent = IntentType.GetOptions;
                }
            );
        }

        internal override Task NotifyAfterAction()
        {
            return TaskHelper.Completed;
        }

        internal override Task<ResultType> ValidateBeforeAction()
        {
            return TaskHelper.ContinueExecution;
        }
    }
}
