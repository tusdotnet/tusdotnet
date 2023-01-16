#if NET6_0_OR_GREATER

using System;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Helpers;

namespace tusdotnet.Runners.TusV1Process
{
    public class CreateFileResponse : TusV1Response
    {
        public string FileId { get; set; }

        public string Location { get; set; }

        public DateTimeOffset? UploadExpires { get; set; }

        internal static CreateFileResponse FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                StatusCode = context.Response.Status,
                ErrorMessage = context.Response.Message,
                Location = context.Response.GetResponseHeaderString(HeaderConstants.Location),
                FileId = context.FileId,
                UploadExpires = context.Response.GetResponseHeaderDateTimeOffset(HeaderConstants.UploadExpires)
            };
        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            commonContext.Response.SetHeader(HeaderConstants.Location, Location.ToString());
            commonContext.FileId = FileId;

            if (UploadExpires is not null)
                commonContext.Response.SetHeader(HeaderConstants.UploadExpires, ExpirationHelper.FormatHeader(UploadExpires));
        }
    }
}

#endif