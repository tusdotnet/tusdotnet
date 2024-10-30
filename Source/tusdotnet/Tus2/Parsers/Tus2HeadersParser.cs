﻿#nullable enable
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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
            var uploadComplete = headers["Upload-Complete"].FirstOrDefault().FromSfBool();
            var uploadToken = headers["Upload-Token"].FirstOrDefault();
            var contentLength = headers.ContentLength;
            var contentType = headers.ContentType;
            var uploadLength = headers["Upload-Length"].FirstOrDefault().FromSfInteger();

            var uploadTokenParser =
                httpContext.RequestServices.GetRequiredService<IUploadTokenParser>();

            return _headers = new()
            {
                UploadOffset = uploadOffset,
                ResourceId = uploadTokenParser.Parse(uploadToken),
                UploadComplete = uploadComplete,
                ContentLength = contentLength,
                ContentType = contentType,
                UploadLength = uploadLength,
            };
        }
    }
}
