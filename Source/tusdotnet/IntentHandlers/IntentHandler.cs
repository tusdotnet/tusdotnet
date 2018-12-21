using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Models;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    internal abstract class IntentHandler
    {
        internal static IntentHandler NotApplicable { get; } = null;

        internal static Requirement[] NoRequirements = new Requirement[0];

        internal LockType LockType { get; }

        internal IntentType Intent { get; }

        internal abstract Requirement[] Requires { get; }

        protected ContextAdapter Context { get; private set; }

        internal abstract Task<ResultType> Invoke();

        protected IntentHandler(ContextAdapter context, IntentType intent, LockType requiresLock)
        {
            Context = context;
            Intent = intent;
            LockType = requiresLock;
        }

        internal async Task<bool> Validate(ContextAdapter context)
        {
            var validator = new Validator(Requires);

            validator.Validate(context);

            if (validator.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            if (validator.StatusCode == HttpStatusCode.NotFound)
            {
                context.Response.NotFound();
                return false;
            }

            await context.Response.Error(validator.StatusCode, validator.ErrorMessage);
            return false;
        }
    }
}
