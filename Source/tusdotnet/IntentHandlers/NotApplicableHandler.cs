using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Models;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
#warning TODO: Remove this class and handle it some other way?
    internal class NotApplicableHandler : IntentHandler
    {
        internal override bool RequiresLock => throw new System.NotImplementedException();

        internal override Requirement[] Requires => throw new System.NotImplementedException();

        internal override IntentType Intent => throw new System.NotImplementedException();

        public NotApplicableHandler() : base(null)
        {
        }

        internal override Task<ResultType> Invoke()
        {
            return Task.FromResult(ResultType.NotHandled);
        }
    }
}
