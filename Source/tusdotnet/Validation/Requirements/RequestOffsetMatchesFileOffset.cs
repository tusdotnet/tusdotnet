using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class RequestOffsetMatchesFileOffset : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            var requestOffset = long.Parse(context.Request.GetHeader(HeaderConstants.UploadOffset));
            var fileOffset = await context.StoreAdapter.GetUploadOffsetAsync(context.Request.FileId, context.CancellationToken);

            if (requestOffset != fileOffset)
            {
                await Conflict($"Offset does not match file. File offset: {fileOffset}. Request offset: {requestOffset}");
            }
        }
    }
}
