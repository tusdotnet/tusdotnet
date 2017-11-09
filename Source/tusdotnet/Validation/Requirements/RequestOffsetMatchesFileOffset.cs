using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class RequestOffsetMatchesFileOffset : Requirement
    {
        public override async Task Validate(ContextAdapter context)
        {
            var requestOffset = long.Parse(context.Request.GetHeader(HeaderConstants.UploadOffset));
            var fileOffset =
                await context.Configuration.Store.GetUploadOffsetAsync(context.GetFileId(), context.CancellationToken);

            if (requestOffset != fileOffset)
            {
                await Conflict($"Offset does not match file. File offset: {fileOffset}. Request offset: {requestOffset}");
            }
        }
    }
}
