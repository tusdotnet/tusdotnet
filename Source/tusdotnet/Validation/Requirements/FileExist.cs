using System.Threading.Tasks;
using tusdotnet.Adapters;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class FileExist : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            var exists = await context.StoreAdapter.FileExistAsync(context.FileId, context.CancellationToken);
            if (!exists)
            {
                await NotFound();
            }
        }
    }
}
