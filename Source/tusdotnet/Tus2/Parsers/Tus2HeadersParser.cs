#nullable enable
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace tusdotnet.Tus2.Parsers
{
    internal class Tus2HeadersParser
    {
        internal static Tus2Headers Parse(HttpContext httpContext)
        {
            var headers = httpContext.Request.Headers;

            var uploadOffset = headers["Upload-Offset"].FirstOrDefault().FromSfInteger();
            var uploadIncomplete = headers["Upload-Incomplete"].FirstOrDefault().FromSfBool();
            var uploadToken = headers["Upload-Token"].FirstOrDefault();

            var uploadTokenParser = httpContext.RequestServices.GetRequiredService<IUploadTokenParser>();

            return new()
            {
                UploadOffset = uploadOffset,
                UploadToken = uploadTokenParser.Parse(uploadToken),
                UploadIncomplete = uploadIncomplete
            };
        }
    }
}
