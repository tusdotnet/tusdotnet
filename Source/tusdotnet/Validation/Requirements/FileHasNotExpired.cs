using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;

namespace tusdotnet.Validation.Requirements
{
    internal class FileHasNotExpired  : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusExpirationStore expirationStore))
            {
                return;
            }

            var expires = await expirationStore.GetExpirationAsync(context.GetFileId(), context.CancellationToken);
            if (expires?.HasPassed() == true)
            {
                await NotFound();
            }
        }
    }
}
