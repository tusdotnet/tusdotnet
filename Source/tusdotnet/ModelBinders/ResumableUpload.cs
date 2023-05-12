#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.ModelBinders
{
    public class ResumableUpload
    {
        public string UploadId { get; set; }

        public Stream Content { get; set; }

        public Dictionary<string, Metadata> Metadata { get; set; }

        public ResumableUpload()
        {
        }

        internal ResumableUpload(string uploadId, Stream content, Dictionary<string, Metadata> metadata)
        {
            UploadId = uploadId;
            Content = content;
            Metadata = metadata;
        }

        public static async ValueTask<ResumableUpload> BindAsync(HttpContext context, ParameterInfo _)
        {
            return await CreateAndBindFromHttpContext<ResumableUpload>(context);
        }

        public static async Task<T> CreateAndBindFromHttpContext<T>(HttpContext context) where T : ResumableUpload, new()
        {
            var upload = new T();
            await upload.BindFromHttpContext(context);

            return upload;
        }

        internal static async Task<object> CreateAndBindFromHttpContext(Type t, HttpContext context)
        {
            var upload = (ResumableUpload)Activator.CreateInstance(t);
            await upload.BindFromHttpContext(context);

            return upload;
        }

        private async Task BindFromHttpContext(HttpContext context)
        {
            var bound = await GenericModelBinder.BindFromHttpContext(context);
            UploadId = bound.UploadId;
            Content = bound.Content;
            Metadata = bound.Metadata;
        }
    }
}

#endif
