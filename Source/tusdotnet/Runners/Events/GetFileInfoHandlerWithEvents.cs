using System.Threading.Tasks;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Runners.Events
{
    internal class GetFileInfoHandlerWithEvents : IntentHandlerWithEvents
    {
        public GetFileInfoHandlerWithEvents(IntentHandler intentHandler)
            : base(intentHandler) { }

        internal override async Task<ResultType> Authorize()
        {
            return await EventHelper.Validate<AuthorizeContext>(
                Context,
                ctx =>
                {
                    ctx.Intent = IntentType.GetFileInfo;
                }
            );
        }

        internal override Task NotifyAfterAction()
        {
            return TaskHelper.Completed;
        }

        internal override Task<ResultType> ValidateBeforeAction()
        {
            return Task.FromResult(ResultType.ContinueExecution);
        }
    }
}
