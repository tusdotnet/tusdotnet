#nullable enable
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Tus2
{
    internal class DefaultMetadataParser : IMetadataParser
    {
        public Dictionary<string, Metadata>? Parse(HttpContext httpContext)
        {
            // Try to parse tus1 Upload-Metadata header.
            if (httpContext.Request.Headers.TryGetValue("Upload-Metadata", out var data))
            {
                var meta = tusdotnet.Parsers.MetadataParser.ParseAndValidate(MetadataParsingStrategy.AllowEmptyValues, data.FirstOrDefault());
                if (meta.Success)
                    return meta.Metadata;
                return null;
            }

            // If Upload-Metadata is not present, try to parse Content-Disposition and Content-Type to get file name and file type.
            var contentType = httpContext.Request.ContentType;
            string? fileName = null;

            if (httpContext.Request.Headers.TryGetValue("Content-Disposition", out var cd))
            {
                var cdhv = ContentDispositionHeaderValue.Parse(cd.FirstOrDefault());
                fileName = cdhv.FileName;
            }

            var hasContentType = !string.IsNullOrWhiteSpace(contentType);
            var hasFileName = !string.IsNullOrWhiteSpace(fileName);

            if (!hasContentType && !hasFileName)
                return null;

            var result = new Dictionary<string, Metadata>();
            if (hasContentType)
                result.Add("ContentType", Metadata.FromBytes(Encoding.UTF8.GetBytes(contentType!)));

            if (hasFileName)
                result.Add("FileName", Metadata.FromBytes(Encoding.UTF8.GetBytes(fileName!)));

            return result.Any() ? result : null;
        }
    }
}
