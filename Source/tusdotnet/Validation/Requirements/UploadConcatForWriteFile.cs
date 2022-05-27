using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Helpers;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadConcatForWriteFile : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!context.StoreAdapter.Extensions.Concatenation)
            {
                return TaskHelper.Completed;
            }

            return ValidateForPatch(context);
        }

        private async Task ValidateForPatch(ContextAdapter context)
        {
            var uploadConcat = await context.StoreAdapter.GetUploadConcatAsync(context.FileId, context.CancellationToken);

            if (uploadConcat is FileConcatFinal)
            {
                await Forbidden("File with \"Upload-Concat: final\" cannot be patched");
            }
        }
    }
}
