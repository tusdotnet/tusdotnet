#if NET6_0_OR_GREATER
using tusdotnet.Adapters;
using tusdotnet.Extensions;

namespace tusdotnet.Runners.TusV1Process
{
    public class DeleteFileResponse : TusV1Response
    {
        internal static DeleteFileResponse FromContextAdapter(ContextAdapter contextAdapter)
        {
            return new()
            {
                StatusCode = contextAdapter.Response.Status,
                ErrorMessage = contextAdapter.Response.Message
            };
        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            // No specifics
        }
    }
}
#endif