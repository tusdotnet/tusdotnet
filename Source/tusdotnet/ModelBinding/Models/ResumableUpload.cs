#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using tusdotnet.ModelBinding.ProtocolHandler;
using tusdotnet.Models;

namespace tusdotnet.ModelBinding.Models
{
    public class ResumableUpload
    {
        public string UploadId { get; set; }

        public Stream Content { get; set; }

        public Dictionary<string, Metadata> Metadata { get; set; }

        public ResumableUpload() { }

        internal ResumableUpload(
            string uploadId,
            Stream content,
            Dictionary<string, Metadata> metadata
        )
        {
            UploadId = uploadId;
            Content = content;
            Metadata = metadata;
        }

        public static async ValueTask<ResumableUpload> BindAsync(
            HttpContext context,
            ParameterInfo _
        )
        {
            return await CreateAndBindFromHttpContext<ResumableUpload>(context);
        }

        public static async Task<T> CreateAndBindFromHttpContext<T>(HttpContext httpContext)
            where T : ResumableUpload, new()
        {
            var file = await ModelBindingHandler.BindFromHttpContext(httpContext);

            if (file is null)
                return null;

            return new()
            {
                UploadId = file.Id,
                Content = await file.GetContentAsync(httpContext.RequestAborted),
                Metadata = await file.GetMetadataAsync(httpContext.RequestAborted)
            };
        }

        internal static async Task<object> CreateAndBindFromHttpContext(
            Type t,
            HttpContext httpContext
        )
        {
            var file = await ModelBindingHandler.BindFromHttpContext(httpContext);

            if (file is null)
                return null;

            var upload = (ResumableUpload)Activator.CreateInstance(t);
            upload.UploadId = file.Id;
            upload.Content = await file.GetContentAsync(httpContext.RequestAborted);
            upload.Metadata = await file.GetMetadataAsync(httpContext.RequestAborted);

            return upload;
        }
    }
}

#endif
