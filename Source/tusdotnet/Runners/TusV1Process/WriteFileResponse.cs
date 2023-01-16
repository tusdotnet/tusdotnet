#if NET6_0_OR_GREATER

using System;
using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;

namespace tusdotnet.Runners.TusV1Process
{
    public class WriteFileResponse : TusV1Response
    {
        public WriteFileResponse(HttpStatusCode statusCode, string errorMessage) : base(statusCode, errorMessage)
        {
        }

        public long UploadOffset { get; set; }

        public DateTimeOffset? UploadExpires { get; set; }

        internal static WriteFileResponse FromContextAdapter(ContextAdapter context)
        {
            var statusCode = context.Response.Status == 0 ? HttpStatusCode.NoContent : context.Response.Status;

            return new(statusCode, context.Response.Message)
            {
                UploadOffset = context.Response.GetResponseHeaderLong(HeaderConstants.UploadOffset, -1).Value,
                UploadExpires = context.Response.GetResponseHeaderDateTimeOffset(HeaderConstants.UploadExpires)
            };
        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            commonContext.Response.SetHeader(HeaderConstants.UploadOffset, UploadOffset.ToString());

            if (UploadExpires is not null)
                commonContext.Response.SetHeader(HeaderConstants.UploadExpires, ExpirationHelper.FormatHeader(UploadExpires));
        }
    }
}

#endif