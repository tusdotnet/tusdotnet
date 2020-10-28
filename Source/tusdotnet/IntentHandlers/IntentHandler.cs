using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Parsers;
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

        protected ContextAdapter Context { get; }

        protected RequestAdapter Request { get; }

        protected ResponseAdapter Response { get; }

        protected CancellationToken CancellationToken { get; }

        protected ITusStore Store { get; }

        internal abstract Task<ResultType> Challenge(UploadChallengeParserResult uploadChallenge, ITusChallengeStoreHashFunction hashFunction, ITusChallengeStore challengeStore);

        internal abstract Task Invoke();

        protected IntentHandler(ContextAdapter context, IntentType intent, LockType requiresLock)
        {
            Context = context;
            Request = context.Request;
            Response = context.Response;
            CancellationToken = context.CancellationToken;
            Store = context.Configuration.Store;

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

            await Context.Response.Error(validator.StatusCode, validator.ErrorMessage);
            return false;
        }
    }
}
