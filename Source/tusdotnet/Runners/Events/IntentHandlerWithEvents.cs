using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;

namespace tusdotnet.Runners.Events
{
    internal abstract class IntentHandlerWithEvents
    {
        internal abstract Task<ResultType> Authorize();

        internal abstract Task<ResultType> ValidateBeforeAction();

        internal abstract Task NotifyAfterAction();

        internal IntentHandler IntentHandler { get; }

        protected ContextAdapter Context => IntentHandler.Context;

        public IntentHandlerWithEvents(IntentHandler intentHandler)
        {
            IntentHandler = intentHandler;
        }
    }
}
