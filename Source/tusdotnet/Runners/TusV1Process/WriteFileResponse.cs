#if NET6_0_OR_GREATER

using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Runners
{
    public class WriteFileResponse : TusV1Response
    {
        public long UploadOffset { get; set; }

        internal static WriteFileResponse FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                StatusCode = context.Response.Status == 0 ? HttpStatusCode.NoContent : context.Response.Status,
                UploadOffset = GetUploadOffset(context.Response)
            };
        }

        internal override void CopySpecificsToResponse(ContextAdapter commonContext)
        {
            commonContext.Response.SetHeader(HeaderConstants.UploadOffset, UploadOffset.ToString());
        }

        private static long GetUploadOffset(ResponseAdapter response)
        {
            return response.Headers.TryGetValue(HeaderConstants.UploadOffset, out var uoString) && long.TryParse(uoString, out var uploadOffset) 
                ? uploadOffset 
                : -1;
        }
    }
}

#endif