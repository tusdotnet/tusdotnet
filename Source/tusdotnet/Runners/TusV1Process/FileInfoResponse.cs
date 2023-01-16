#if NET6_0_OR_GREATER
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Parsers;

namespace tusdotnet.Runners.TusV1Process
{
    public class FileInfoResponse : TusV1Response
    {
        public FileInfoResponse(HttpStatusCode statusCode, string? errorMessage) : base(statusCode, errorMessage)
        {
        }

        public long? UploadLength { get; set; }

        public long UploadOffset { get; set; }

        public FileConcat? UploadConcat { get; set; }

        public bool UploadDeferLength { get; set; }

        public Dictionary<string, Metadata>? Metadata { get; set; }

        public string? MetadataString { get; set; }

        internal static FileInfoResponse FromContextAdapter(ContextAdapter context)
        {
            var response = new FileInfoResponse(context.Response.Status, context.Response.Message);

            if (context.Response.Headers.TryGetValue(HeaderConstants.UploadOffset, out var offsetString))
                response.UploadOffset = long.Parse(offsetString);

            if (context.Response.Headers.TryGetValue(HeaderConstants.UploadMetadata, out var um))
                response.MetadataString = um;

            if (context.Response.Headers.TryGetValue(HeaderConstants.UploadLength, out var ulString))
                response.UploadLength = long.Parse(ulString);
            else
                response.UploadDeferLength = true;

            if (context.Response.Headers.TryGetValue(HeaderConstants.UploadConcat, out var concat))
                response.UploadConcat = new UploadConcat(concat).Type;

            if (response.MetadataString is not null)
                response.Metadata = MetadataParser.ParseAndValidate(MetadataParsingStrategy.AllowEmptyValues, response.MetadataString).Metadata;

            return response;

        }

        internal override void CopySpecificsToCommonContext(ContextAdapter commonContext)
        {
            var response = commonContext.Response;

            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
            response.SetHeader(HeaderConstants.UploadOffset, UploadOffset.ToString());

            if (UploadDeferLength)
                response.SetHeader(HeaderConstants.UploadDeferLength, "1");
            else
                response.SetHeader(HeaderConstants.UploadLength, UploadLength!.Value.ToString());

            if (MetadataString is not null)
                response.SetHeader(HeaderConstants.UploadMetadata, MetadataString);

            if (UploadConcat is not null)
            {
                if (UploadConcat is FileConcatFinal final)
                {
                    final.AddUrlPathToFiles(commonContext.ConfigUrlPath);

                    // "The response to a HEAD request for a final upload SHOULD NOT contain the Upload-Offset header unless the concatenation has been successfully finished."
                    if (UploadLength != UploadOffset)
                        response.Headers.Remove(HeaderConstants.UploadOffset);

                }

                response.SetHeader(HeaderConstants.UploadConcat, UploadConcat.GetHeader());
            }

            // TODO Shouldn't expires be included?
        }
    }
}
#endif