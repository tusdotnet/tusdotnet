using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;

namespace tusdotnet.Validation.Requirements
{
    internal class FileHasNotExpired  : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusExpirationStore expirationStore))
            {
                return Task.FromResult(0);
            }

            return ValidateInternal(context, expirationStore);
        }

        private async Task ValidateInternal(ContextAdapter context, ITusExpirationStore expirationStore)
        {
            var expires = await expirationStore.GetExpirationAsync(context.Request.FileId, context.CancellationToken);
            if (expires?.HasPassed() == true)
            {
                await NotFound();
            }
        }
    }
}
