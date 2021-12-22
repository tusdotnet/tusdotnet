#nullable enable

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace tusdotnet.Tus2
{
    public class Tus2Headers
    {
        public long? UploadOffset { get; set; }

        public string? UploadToken { get; set; }

        public bool? UploadIncomplete { get; set; }

        internal static Tus2Headers Parse(HttpContext httpContext)
        {
            var headers = httpContext.Request.Headers;

            var couldParseUploadOffset = long.TryParse(headers["Upload-Offset"].FirstOrDefault(), out long uploadOffset);

            var uploadIncomplete = ParseUploadIncomplete(headers["Upload-Incomplete"].FirstOrDefault());

            var uploadTokenParser = httpContext.RequestServices.GetRequiredService<IUploadTokenParser>();

            return new()
            {
                UploadOffset = couldParseUploadOffset ? uploadOffset : null,
                UploadToken = uploadTokenParser.Parse(headers["Upload-Token"].FirstOrDefault()),
                UploadIncomplete = uploadIncomplete
            };
        }

        private static bool? ParseUploadIncomplete(string? uploadIncomplete)
        {
            if (uploadIncomplete == null)
                return null;

             return uploadIncomplete == "?1";
        }
    }
}
