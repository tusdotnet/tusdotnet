#if NET6_0_OR_GREATER

using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Runners;

namespace tusdotnet.Runners.TusV1Process
{
    public class CreateFileResponse : TusV1Response
    {
        public string FileId { get; set; }

        public string Location { get; set; }

        internal static CreateFileResponse FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                StatusCode = context.Response.Status,
                ErrorMessage = context.Response.Message,
                Location = context.Response.Headers[HeaderConstants.Location],
                FileId = context.FileId
            };
        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            commonContext.Response.SetHeader(HeaderConstants.Location, Location.ToString());

            commonContext.FileId = FileId;
        }
    }
}

#endif