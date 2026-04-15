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

        internal abstract IntentHandler IntentHandler { get; }

        protected ContextAdapter Context => IntentHandler.Context;
    }

    internal abstract class IntentHandlerWithEvents<T> : IntentHandlerWithEvents
        where T : IntentHandler
    {
        private readonly T _typedIntentHandler;

        internal override IntentHandler IntentHandler => _typedIntentHandler;

        internal T TypedIntentHandler => _typedIntentHandler;

        public IntentHandlerWithEvents(T intentHandler)
        {
            _typedIntentHandler = intentHandler;
        }
    }
}
