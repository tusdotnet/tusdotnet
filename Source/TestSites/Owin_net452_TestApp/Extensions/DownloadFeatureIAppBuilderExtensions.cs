using System;
using System.IO;
using System.Text;
using Owin;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace Owin_net452_TestApp.Extensions
{
    public static class DownloadFeatureIAppBuilderExtensions
    {
        /// <summary>
        /// Use a simple middleware that allows downloading of files from a tusdotnet store.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="tusConfiguration"></param>

        public static void SetupDownloadFeature(this IAppBuilder app, DefaultTusConfiguration tusConfiguration)
        {
            app.Use(async (context, next) =>
            {
                if (!context.Request.Method.Equals("get", StringComparison.InvariantCultureIgnoreCase))
                {
                    await next.Invoke();
                    return;
                }

                if (context.Request.Uri.LocalPath.StartsWith(tusConfiguration.UrlPath, StringComparison.Ordinal))
                {
                    var fileId = context.Request.Uri.LocalPath.Replace(tusConfiguration.UrlPath, "").Trim('/', ' ');
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        var readableStore = (ITusReadableStore)tusConfiguration.Store;
                        var file = await readableStore.GetFileAsync(fileId, context.Request.CallCancelled);

                        if (file == null)
                        {
                            context.Response.StatusCode = 404;
                            await context.Response.WriteAsync($"File with id {fileId} was not found.",
                                context.Request.CallCancelled);
                            return;
                        }

                        var fileStream = await file.GetContentAsync(context.Request.CallCancelled);
                        var metadata = await file.GetMetadataAsync(context.Request.CallCancelled);

                        context.Response.ContentType = metadata.ContainsKey("contentType")
                            ? metadata["contentType"].GetString(Encoding.UTF8)
                            : "application/octet-stream";

                        context.Response.ContentLength = fileStream.Length;

                        if (metadata.TryGetValue("name", out var nameMetadata))
                        {
                            context.Response.Headers.Add("Content-Disposition",
                                new[] { $"attachment; filename=\"{nameMetadata.GetString(Encoding.UTF8)}\"" });
                        }

                        using (fileStream)
                        {
                            await fileStream.CopyToAsync(context.Response.Body, 81920, context.Request.CallCancelled);
                        }
                        return;
                    }
                }

                switch (context.Request.Uri.LocalPath)
                {
                    case "/":
                        context.Response.ContentType = "text/html";
                        await context.Response.WriteAsync(File.ReadAllText("../../wwwroot/index.html"),
                            context.Request.CallCancelled);
                        break;
                    case "/tus.min.js":
                        context.Response.ContentType = "application/js";
                        await context.Response.WriteAsync(File.ReadAllText("../../wwwroot/tus.min.js"),
                            context.Request.CallCancelled);
                        break;
                    case "/index.js":
                        context.Response.ContentType = "application/js";
                        await context.Response.WriteAsync(File.ReadAllText("../../wwwroot/index.js"),
                            context.Request.CallCancelled);
                        break;
                    case "/index.css":
                        context.Response.ContentType = "text/css";
                        await context.Response.WriteAsync(File.ReadAllText("../../wwwroot/index.css"),
                            context.Request.CallCancelled);
                        break;
                    default:
                        context.Response.StatusCode = 404;
                        break;
                }
            });
        }
    }
}
