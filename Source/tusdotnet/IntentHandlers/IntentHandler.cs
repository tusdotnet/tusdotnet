using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    internal abstract class IntentHandler
    {
        internal static IntentHandler NotApplicable { get; } = null;

        internal static Requirement[] NoRequirements { get; } = new Requirement[0];

        internal LockType LockType { get; }

        internal IntentType Intent { get; }

        internal abstract Requirement[] Requires { get; }

        internal ContextAdapter Context { get; }

        protected RequestAdapter Request => Context.Request;

        protected ResponseAdapter Response => Context.Response;

        protected CancellationToken CancellationToken => Context.CancellationToken;

        protected ITusStore Store => StoreAdapter.Store;

        protected StoreAdapter StoreAdapter => Context.StoreAdapter;

        internal abstract Task Invoke();

        protected IntentHandler(ContextAdapter context, IntentType intent, LockType requiresLock)
        {
            Context = context;

            Intent = intent;
            LockType = requiresLock;
        }

        internal async Task<bool> Validate()
        {
            var validator = new Validator(Requires);

            await validator.Validate(Context);

            if (validator.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            if (validator.StatusCode == HttpStatusCode.NotFound)
            {
                Context.Response.NotFound();
                return false;
            }

            Context.Response.Error(validator.StatusCode, validator.ErrorMessage);
            return false;
        }
    }
}
