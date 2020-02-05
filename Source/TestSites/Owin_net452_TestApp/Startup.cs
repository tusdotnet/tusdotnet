using System;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Microsoft.Owin;
using Owin;
using Owin_net452_TestApp.Extensions;
using OwinTestApp;
using tusdotnet;
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
            // Change the value of EnableOnAuthorize in app.config to enable or disable
            // the new authorization event.
            var enableAuthorize = Convert.ToBoolean(ConfigurationManager.AppSettings["EnableOnAuthorize"]);

            var tusConfiguration = CreateTusConfiguration(enableAuthorize);

            if (enableAuthorize)
            {
                app.SetupSimpleBasicAuth();
            }

            app.SetupCorsPolicy();

            app.SetupSimpleExceptionHandler();

            // owinRequest parameter can be used to create a tus configuration based on current user, domain, host, port or whatever.
            // In this case we just return the same configuration for everyone.
            app.UseTus(owinRequest => tusConfiguration);

            // All GET requests to tusdotnet are forwarded so that you can handle file downloads.
            // This is done because the file's metadata is domain specific and thus cannot be handled 
            // in a generic way by tusdotnet.
            app.SetupDownloadFeature(tusConfiguration);

            // Setup cleanup job to remove incomplete expired files.
            app.StartCleanupJob(tusConfiguration);
        }

        private static DefaultTusConfiguration CreateTusConfiguration(bool enableAuthorize)
        {
            return new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = new TusDiskStore(@"C:\tusfiles\"),
                MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
                Events = new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        var completedTask = Task.FromResult(0);

                        if (!enableAuthorize)
                            return completedTask;

                        if (ctx.OwinContext.Authentication.User?.Identity?.IsAuthenticated != true)
                        {
                            ctx.OwinContext.Response.Headers.Add("WWW-Authenticate", new StringValues("Basic realm=\"tusdotnet-test-owin\""));
                            ctx.FailRequest(HttpStatusCode.Unauthorized);
                            return completedTask;
                        }

                        if (ctx.OwinContext.Authentication.User.Identity.Name != "test")
                        {
                            ctx.FailRequest(HttpStatusCode.Forbidden, "'test' is the only allowed user");
                            return completedTask;
                        }

                        // Do other verification on the user; claims, roles, etc.

                        // Verify different things depending on the intent of the request.
                        // E.g.:
                        //   Does the file about to be written belong to this user?
                        //   Is the current user allowed to create new files or have they reached their quota?
                        //   etc etc
                        switch (ctx.Intent)
                        {
                            case IntentType.CreateFile:
                                break;
                            case IntentType.ConcatenateFiles:
                                break;
                            case IntentType.WriteFile:
                                break;
                            case IntentType.DeleteFile:
                                break;
                            case IntentType.GetFileInfo:
                                break;
                            case IntentType.GetOptions:
                                break;
                            default:
                                break;
                        }

                        return completedTask;
                    },

                    OnBeforeCreateAsync = ctx =>
                    {
                        // Partial files are not complete so we do not need to validate
                        // the metadata in our example.
                        if (ctx.FileConcatenation is FileConcatPartial)
                        {
                            return Task.FromResult(true);
                        }

                        if (!ctx.Metadata.ContainsKey("name") || ctx.Metadata["name"].HasEmptyValue)
                        {
                            ctx.FailRequest("name metadata must be specified. ");
                        }

                        if (!ctx.Metadata.ContainsKey("contentType") || ctx.Metadata["contentType"].HasEmptyValue)
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
                        //var file = await ctx.GetFileAsync();
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
