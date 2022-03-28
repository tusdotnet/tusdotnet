#nullable enable
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace tusdotnet.Tus2
{
    internal class Tus2HeadersParser : IHeaderParser
    {
        private Tus2Headers _headers = null!;

        public Tus2Headers Parse(HttpContext httpContext)
        {
            if (_headers != null)
                return _headers;

            var headers = httpContext.Request.Headers;

            var uploadOffset = headers["Upload-Offset"].FirstOrDefault().FromSfInteger();
            var uploadIncomplete = headers["Upload-Incomplete"].FirstOrDefault().FromSfBool();
            var uploadToken = headers["Upload-Token"].FirstOrDefault();

            var uploadTokenParser = httpContext.RequestServices.GetRequiredService<IUploadTokenParser>();

            return _headers = new()
            {
                UploadOffset = uploadOffset,
                UploadToken = uploadTokenParser.Parse(uploadToken),
                UploadIncomplete = uploadIncomplete
            };
        }
    }
}
