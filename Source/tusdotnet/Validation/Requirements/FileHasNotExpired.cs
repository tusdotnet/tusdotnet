using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Helpers;

namespace tusdotnet.Validation.Requirements
{
    internal class FileHasNotExpired  : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!context.StoreAdapter.Extensions.Expiration)
            {
                return TaskHelper.Completed;
            }

            return ValidateInternal(context);
        }

        private async Task ValidateInternal(ContextAdapter context)
        {
            var expires = await context.StoreAdapter.GetExpirationAsync(context.FileId, context.CancellationToken);
            if (expires?.HasPassed() == true)
            {
                await NotFound();
            }
        }
    }
}
