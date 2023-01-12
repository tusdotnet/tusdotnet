#if NET6_0_OR_GREATER
#nullable enable
using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Runners.TusV1Process
{
    public abstract class TusV1Response
    {
        public HttpStatusCode StatusCode { get; set; }

        public string? ErrorMessage { get; set; }

        internal void CopyToCommonContext(ContextAdapter commonContext)
        {
            commonContext.Response.SetResponse(StatusCode, ErrorMessage);
            commonContext.Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);

            CopySpecificsToResponse(commonContext);
        }

        internal abstract void CopySpecificsToResponse(ContextAdapter commonContext);
    }
}

#endif