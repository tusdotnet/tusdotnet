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

        public TusV1Response(HttpStatusCode statusCode, string? errorMessage)
        {
            StatusCode = statusCode;
            ErrorMessage = errorMessage;
        }

        internal void CopyToCommonContext(ContextAdapter commonContext)
        {
            commonContext.Response.SetResponse(StatusCode, ErrorMessage);
            commonContext.Response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);

            CopySpecificsToCommonContext(commonContext);
        }

        internal abstract void CopySpecificsToCommonContext(ContextAdapter commonContext);
    }
}

#endif