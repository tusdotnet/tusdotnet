#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;

namespace tusdotnet.Runners.TusV1Process
{
    public class ServerOptionsResponse : TusV1Response
    {
        public string TusVersion { get; set; }

        public long? FileMaxSize { get; set; }

        public IEnumerable<string> Extensions { get; set; }

        public IEnumerable<string> SupportedChecksumAlgorithms { get; set; }

        internal static ServerOptionsResponse FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                StatusCode = context.Response.Status,
                ErrorMessage = context.Response.Message,
                TusVersion = context.Response.GetResponseHeaderString(HeaderConstants.TusVersion),
                FileMaxSize = context.Response.GetResponseHeaderLong(HeaderConstants.TusMaxSize),
                Extensions = context.Response.GetResponseHeaderList(HeaderConstants.TusExtension),
                SupportedChecksumAlgorithms = context.Response.GetResponseHeaderList(HeaderConstants.TusChecksumAlgorithm)
            };
        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            commonContext.Response.SetHeader(HeaderConstants.TusVersion, TusVersion);
            commonContext.Response.SetHeader(HeaderConstants.TusResumable, TusVersion);

            if (FileMaxSize is not null)
                commonContext.Response.SetHeader(HeaderConstants.TusMaxSize, FileMaxSize.ToString());

            if (Extensions?.Any() == true)
                commonContext.Response.SetHeader(HeaderConstants.TusExtension, string.Join(",", Extensions));

            if (SupportedChecksumAlgorithms?.Any() == true)
                commonContext.Response.SetHeader(HeaderConstants.TusChecksumAlgorithm, string.Join(",", SupportedChecksumAlgorithms));
        }
    }
}
#endif