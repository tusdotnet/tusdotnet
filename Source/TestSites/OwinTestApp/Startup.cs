using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Owin;
using OwinTestApp;
using tusdotnet;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

[assembly: OwinStartup(typeof(Startup))]

namespace OwinTestApp
{
    public class Startup
    {
        private readonly AbsoluteExpiration _absoluteExpiration = new AbsoluteExpiration(TimeSpan.FromMinutes(5));
        private readonly TusDiskStore _tusDiskStore = new TusDiskStore(@"C:\tusfiles\");

        public void Configuration(IAppBuilder app)
        {
            var corsPolicy = new System.Web.Cors.CorsPolicy
            {
                AllowAnyHeader = true,
                AllowAnyMethod = true,
                AllowAnyOrigin = true
            };

            // ReSharper disable once PossibleNullReferenceException - nameof will cause compiler error if the property does not exist.
            corsPolicy.GetType()
                .GetProperty(nameof(corsPolicy.ExposedHeaders))
                .SetValue(corsPolicy, CorsHelper.GetExposedHeaders());

            app.UseCors(new CorsOptions
            {
                PolicyProvider = new CorsPolicyProvider
                {
                    PolicyResolver = context => Task.FromResult(corsPolicy)
                }
            });

            app.Use(async (context, next) =>
            {
                try
                {
                    await next.Invoke();
                }
                catch (Exception exc)
                {
                    Console.Error.WriteLine(exc);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("An internal server error has occurred");
                }
            });

            app.UseTus(request =>
            {
                return new DefaultTusConfiguration
                {
                    Store = _tusDiskStore,
                    UrlPath = "/files",
                    Events = new Events
                    {
                        OnFileCompleteAsync = ctx =>
                        {
                            Console.WriteLine(
                                $"Upload of {ctx.FileId} is complete. Callback also got a store of type {ctx.Store.GetType().FullName}");
                            // If the store implements ITusReadableStore one could access the completed file here.
                            // The default TusDiskStore implements this interface:
                            //var file = await((tusdotnet.Interfaces.ITusReadableStore)ctx.Store).GetFileAsync(ctx.FileId, ctx.CancellationToken);
                            return Task.FromResult(true);
                        }
                    },
                    // Set an expiration time where incomplete files can no longer be updated.
                    // This value can either be absolute or sliding.
                    // Absolute expiration will be saved per file on create
                    // Sliding expiration will be saved per file on create and updated on each patch/update.
                    Expiration = _absoluteExpiration
                };
            });

            app.Use(async (context, next) =>
            {
                // All GET requests to tusdotnet are forwared so that you can handle file downloads.
                // This is done because the file's metadata is domain specific and thus cannot be handled 
                // in a generic way by tusdotnet.
                if (!context.Request.Method.Equals("get", StringComparison.InvariantCultureIgnoreCase))
                {
                    await next.Invoke();
                    return;
                }

                if (context.Request.Uri.LocalPath.StartsWith("/files/", StringComparison.Ordinal))
                {
                    var fileId = context.Request.Uri.LocalPath.Replace("/files/", "").Trim();
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        var file = await _tusDiskStore.GetFileAsync(fileId, context.Request.CallCancelled);

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

                        if (metadata.ContainsKey("name"))
                        {
                            var name = metadata["name"].GetString(Encoding.UTF8);
                            context.Response.Headers.Add("Content-Disposition",
                                new[] {$"attachment; filename=\"{name}\""});
                        }

                        await fileStream.CopyToAsync(context.Response.Body, 81920, context.Request.CallCancelled);
                        return;
                    }
                }

                switch (context.Request.Uri.LocalPath)
                {
                    case "/":
                        context.Response.ContentType = "text/html";
                        await context.Response.WriteAsync(File.ReadAllText("../../index.html"),
                            context.Request.CallCancelled);
                        break;
                    case "/tus.js":
                        context.Response.ContentType = "application/js";
                        await context.Response.WriteAsync(File.ReadAllText("../../tus.js"),
                            context.Request.CallCancelled);
                        break;
                    default:
                        context.Response.StatusCode = 404;
                        break;
                }
            });

            // Setup cleanup job to remove incomplete expired files.
            // This is just a simple example. In production one would use a cronjob/webjob and poll an endpoint that runs RemoveExpiredFilesAsync.
            var onAppDisposingToken = new OwinContext(app.Properties).Get<CancellationToken>("host.OnAppDisposing");
            Task.Run(async () =>
            {
                while (!onAppDisposingToken.IsCancellationRequested)
                {
                    Console.WriteLine("Running cleanup job...");
                    var numberOfRemovedFiles = await _tusDiskStore.RemoveExpiredFilesAsync(onAppDisposingToken);
                    Console.WriteLine(
                        $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {_absoluteExpiration.Timeout.TotalMilliseconds} ms");
                    await Task.Delay(_absoluteExpiration.Timeout, onAppDisposingToken);
                }
            }, onAppDisposingToken);
        }
    }
}
