using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Validation;

namespace tusdotnet.ProtocolHandlers
{
    internal abstract class ProtocolMethodHandler
    {
        internal abstract bool RequiresLock { get; }

        internal abstract Requirement[] Requires { get; }

        internal abstract bool CanHandleRequest(ContextAdapter context);

        internal abstract Task<bool> Handle(ContextAdapter context);

        internal Task<bool> Validate(ContextAdapter context)
        {
            var validator = new Validator(Requires);

            validator.Validate(context);

            if (validator.StatusCode == HttpStatusCode.OK)
            {
                return Task.FromResult(true);
            }

            if (validator.StatusCode == HttpStatusCode.NotFound)
            {
                context.Response.NotFound();
                return Task.FromResult(false);
            }

            return RespondWithError();

            async Task<bool> RespondWithError()
            {
                await context.Response.Error(validator.StatusCode, validator.ErrorMessage);
                return false;
            }
        }
    }
}
