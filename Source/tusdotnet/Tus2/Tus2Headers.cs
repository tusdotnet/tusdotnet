#nullable enable

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal class Tus2Headers
    {
        public long? UploadOffset { get; set; }

        public string? UploadToken { get; set; }

        public bool? UploadIncomplete { get; set; }

        internal static Tus2Headers Parse(HttpContext httpContext)
        {
            var headers = httpContext.Request.Headers;

            var couldParseUploadOffset = long.TryParse(headers["Upload-Offset"].FirstOrDefault(), out long uploadOffset);
            var uploadIncomplete = headers["Upload-Incomplete"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase);

            return new()
            {
                UploadOffset = couldParseUploadOffset ? uploadOffset : null,
                UploadToken = headers["Upload-Token"].FirstOrDefault(),
                UploadIncomplete = uploadIncomplete
            };
        }
    }
}
