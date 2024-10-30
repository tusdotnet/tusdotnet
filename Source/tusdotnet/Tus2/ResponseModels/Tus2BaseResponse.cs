﻿using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace tusdotnet.Tus2
{
    public abstract class Tus2BaseResponse
    {
        public HttpStatusCode Status { get; set; }

        public string ErrorMessage { get; set; }

        public bool DisconnectClient { get; set; }

        public long? UploadOffset { get; set; }

        public bool IsError
        {
            get
            {
                var statusCode = (int)Status;
                return statusCode > 299; // TODO no support for redirects
            }
        }

        protected bool NoCache { get; set; }

        internal async Task WriteTo(HttpContext httpContext)
        {
            if (DisconnectClient)
            {
                httpContext.Abort();
                return;
            }

            if (NoCache)
            {
                httpContext.SetHeader("Cache-Control", "no-store");
            }

            if (UploadOffset != null)
            {
                httpContext.SetHeader("Upload-Offset", UploadOffset.ToString());
            }

            if (IsError)
            {
                await httpContext.Error(Status, ErrorMessage);
                return;
            }

            httpContext.SetHeader("upload-draft-interop-version", "6");

            httpContext.Response.StatusCode = (int)Status;

            await WriteResponse(httpContext);
        }

        protected abstract Task WriteResponse(HttpContext context);
    }
}
