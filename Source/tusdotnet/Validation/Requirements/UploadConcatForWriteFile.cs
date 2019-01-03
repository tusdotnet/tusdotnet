using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Extensions;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadConcatForWriteFile : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!(context.Configuration.Store is ITusConcatenationStore concatStore))
            {
                return TaskHelper.Completed;
            }

            return ValidateForPatch(context, concatStore);
        }

        private async Task ValidateForPatch(ContextAdapter context, ITusConcatenationStore concatStore)
        {
            var uploadConcat = await concatStore.GetUploadConcatAsync(context.GetFileId(), context.CancellationToken);

            if (uploadConcat is FileConcatFinal)
            {
                await Forbidden("File with \"Upload-Concat: final\" cannot be patched");
            }
        }
    }
}
