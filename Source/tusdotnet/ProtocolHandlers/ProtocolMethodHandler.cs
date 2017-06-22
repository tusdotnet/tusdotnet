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

        internal abstract Specification[] Specifications { get; }

        internal abstract bool CanHandleRequest(ContextAdapter context);

        internal abstract Task<bool> Handle(ContextAdapter context);

        internal async Task<bool> Validate(ContextAdapter context)
        {
            var validator = new Validator(Specifications);

            validator.Validate(context);

            if (validator.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            if (validator.StatusCode == HttpStatusCode.NotFound)
            {
                context.Response.NotFound();
            }
            else
            {
                await context.Response.Error(validator.StatusCode, validator.ErrorMessage);
            }

            return false;
        }
    }
}
