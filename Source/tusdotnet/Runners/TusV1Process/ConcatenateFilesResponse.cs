#if NET6_0_OR_GREATER

using System;
using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;

namespace tusdotnet.Runners.TusV1Process
{
    public class ConcatenateFilesResponse : TusV1Response
    {
        public ConcatenateFilesResponse(HttpStatusCode statusCode, string errorMessage) : base(statusCode, errorMessage)
        {
        }

        public string FileId { get; set; }

        internal static ConcatenateFilesResponse FromContextAdapter(ContextAdapter context)
        {
            return new(context.Response.Status, context.Response.Message)
            {
                FileId = context.FileId,
            };
        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            commonContext.Response.SetHeader(HeaderConstants.Location, commonContext.ConfigUrlPath + "/" + FileId);
            commonContext.FileId = FileId;
        }
    }
}

#endif