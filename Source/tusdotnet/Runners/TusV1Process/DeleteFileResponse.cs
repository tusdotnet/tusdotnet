#if NET6_0_OR_GREATER
using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Extensions;

namespace tusdotnet.Runners.TusV1Process
{
    public class DeleteFileResponse : TusV1Response
    {
        public DeleteFileResponse(HttpStatusCode statusCode, string errorMessage) 
            : base(statusCode, errorMessage)
        {
        }

        internal static DeleteFileResponse FromContextAdapter(ContextAdapter contextAdapter)
        {
            return new(contextAdapter.Response.Status, contextAdapter.Response.Message);
        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            // No specifics
        }
    }
}
#endif