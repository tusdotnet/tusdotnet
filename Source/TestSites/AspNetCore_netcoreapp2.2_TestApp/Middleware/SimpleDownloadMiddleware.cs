using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace AspNetCore_netcoreapp2_2_TestApp.Middleware
{
    public class SimpleDownloadMiddleware
    {
        private readonly RequestDelegate next;
        private readonly Func<HttpContext, Task<DefaultTusConfiguration>> _configFactory;

        public SimpleDownloadMiddleware(RequestDelegate next, Func<HttpContext, Task<DefaultTusConfiguration>> configFactory)
        {
            this.next = next;
            _configFactory = configFactory;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Method.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                await next.Invoke(context);
                return;
            }

            var config = await _configFactory(context);
            var readableStore = config.Store as ITusReadableStore;
            if (readableStore == null)
            {
                await next.Invoke(context);
                return;
            }

            var url = new Uri(context.Request.GetDisplayUrl());

            if (url.LocalPath.StartsWith(config.UrlPath, StringComparison.Ordinal))
            {
                var fileId = url.LocalPath.Replace(config.UrlPath, "").Trim('/', ' ');
                if (!string.IsNullOrEmpty(fileId))
                {
                    var file = await readableStore.GetFileAsync(fileId, context.RequestAborted);

                    if (file == null)
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync($"File with id {fileId} was not found.",
                            context.RequestAborted);
                        return;
                    }

                    var fileStream = await file.GetContentAsync(context.RequestAborted);
                    var metadata = await file.GetMetadataAsync(context.RequestAborted);

                    context.Response.ContentType = metadata.ContainsKey("contentType")
                        ? metadata["contentType"].GetString(Encoding.UTF8)
                        : "application/octet-stream";

                    if (metadata.TryGetValue("name", out var nameMeta))
                    {
                        context.Response.Headers.Add("Content-Disposition",
                            new[] { $"attachment; filename=\"{nameMeta.GetString(Encoding.UTF8)}\"" });
                    }

                    using (fileStream)
                    {
                        await fileStream.CopyToAsync(context.Response.Body, 81920, context.RequestAborted);
                    }
                }
            }
        }
    }

    public static class SimpleDownloadMiddlewareExtensions
    {
        /// <summary>
        /// Use a simple middleware that allows downloading of files from a tusdotnet store.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configFactory"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseSimpleDownloadMiddleware(this IApplicationBuilder builder, Func<HttpContext, Task<DefaultTusConfiguration>> configFactory)
        {
            return builder.UseMiddleware<SimpleDownloadMiddleware>(configFactory);
        }
    }
}
