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
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

[assembly: OwinStartup(typeof(Startup))]

namespace OwinTestApp
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var tusConfiguration = CreateTusConfiguration();

            SetupCorsPolicy(app);

            SetupSimpleExceptionHandler(app);

            // owinRequest parameter can be used to create a tus configuration based on current user, domain, host, port or whatever.
            // In this case we just return the same configuration for everyone.
            app.UseTus(owinRequest => tusConfiguration);

            // All GET requests to tusdotnet are forwared so that you can handle file downloads.
            // This is done because the file's metadata is domain specific and thus cannot be handled 
            // in a generic way by tusdotnet.
            SetupDownloadFeature(app, tusConfiguration);

            // Setup cleanup job to remove incomplete expired files.
            StartCleanupJob(app, tusConfiguration);
        }

        private void SetupCorsPolicy(IAppBuilder app)
        {
            var corsPolicy = new System.Web.Cors.CorsPolicy
            {
                AllowAnyHeader = true,
                AllowAnyMethod = true,
                AllowAnyOrigin = true
            };

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
        }

        /// <summary>
        /// Use a simple exception handler that will log errors and return 500 internal server error on exceptions.
        /// </summary>
        /// <param name="app"></param>
        private static void SetupSimpleExceptionHandler(IAppBuilder app)
        {
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
        }

        /// <summary>
        /// Use a simple middleware that allows downloading of files from a tusdotnet store.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="tusConfiguration"></param>

        private static void SetupDownloadFeature(IAppBuilder app, DefaultTusConfiguration tusConfiguration)
        {
            var readableStore = (ITusReadableStore)tusConfiguration.Store;

            app.Use(async (context, next) =>
            {
                if (!context.Request.Method.Equals("get", StringComparison.InvariantCultureIgnoreCase))
                {
                    await next.Invoke();
                    return;
                }

                if (context.Request.Uri.LocalPath.StartsWith(tusConfiguration.UrlPath, StringComparison.Ordinal))
                {
                    var fileId = context.Request.Uri.LocalPath.Replace(tusConfiguration.UrlPath, "").Trim();
                    if (!string.IsNullOrEmpty(fileId))
                    {
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
        }

        private static void StartCleanupJob(IAppBuilder app, DefaultTusConfiguration tusConfiguration)
        {
            var expirationStore = (ITusExpirationStore)tusConfiguration.Store;
            var expiration = tusConfiguration.Expiration;
            var onAppDisposingToken = new OwinContext(app.Properties).Get<CancellationToken>("host.OnAppDisposing");
            Task.Run(async () =>
            {
                while (!onAppDisposingToken.IsCancellationRequested)
                {
                    Console.WriteLine("Running cleanup job...");

                    var numberOfRemovedFiles = await expirationStore.RemoveExpiredFilesAsync(onAppDisposingToken);

                    Console.WriteLine(
                        $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {expiration.Timeout.TotalMilliseconds} ms");

                    await Task.Delay(expiration.Timeout, onAppDisposingToken);
                }
            }, onAppDisposingToken);
        }

        private static DefaultTusConfiguration CreateTusConfiguration()
        {
            return new DefaultTusConfiguration
            {
                Store = new TusDiskStore(@"C:\tusfiles\"),
                UrlPath = "/files",
                Events = new Events
                {
                    OnBeforeCreateAsync = ctx =>
                    {
                        // Partial files are not complete so we do not need to validate
                        // the metadata in our example.
                        if (ctx.FileConcatenation is FileConcatPartial)
                        {
                            return Task.FromResult(true);
                        }

                        if (!ctx.Metadata.ContainsKey("name"))
                        {
                            ctx.FailRequest("name metadata must be specified. ");
                        }

                        if (!ctx.Metadata.ContainsKey("contentType"))
                        {
                            ctx.FailRequest("contentType metadata must be specified. ");
                        }

                        return Task.FromResult(true);
                    },
                    OnCreateCompleteAsync = ctx =>
                    {
                        Console.WriteLine($"Created file {ctx.FileId} using {ctx.Store.GetType().FullName}");
                        return Task.FromResult(true);
                    },
                    OnBeforeDeleteAsync = ctx =>
                    {
                        // Can the file be deleted? If not call ctx.FailRequest(<message>);
                        return Task.FromResult(true);
                    },
                    OnDeleteCompleteAsync = ctx =>
                    {
                        Console.WriteLine($"Deleted file {ctx.FileId} using {ctx.Store.GetType().FullName}");
                        return Task.FromResult(true);
                    },
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
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(5))
            };
        }
    }
}
