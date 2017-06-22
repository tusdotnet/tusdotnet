using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;

namespace tusdotnet.Validation.Specifications
{
    internal class FileExist : Specification
    {
        public override async Task Validate(ContextAdapter context)
        {
            var exists = await context.Configuration.Store.FileExistAsync(context.GetFileId(), context.CancellationToken);
            if (!exists)
            {
                await NotFound();
            }
        }
    }
}
