using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Models;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
#warning TODO: Move all abstract and virtual properties to constructor?
    internal abstract class IntentHandler
    {
        public static NotApplicableHandler NotApplicable { get; } = new NotApplicableHandler();

#warning TODO: Change to LockType.NoLock or LockType.RequireLock
        internal abstract bool RequiresLock { get; }

        internal abstract IntentType Intent { get; }

        protected bool ShouldValidateTusResumableHeader { get; set; }

        internal abstract Requirement[] Requires { get; }

        protected ContextAdapter Context { get; private set; }

        internal abstract Task<ResultType> Invoke();

        protected string RequestFileId => Context.FileId;

        protected IntentHandler(ContextAdapter context)
        {
            Context = context;
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
