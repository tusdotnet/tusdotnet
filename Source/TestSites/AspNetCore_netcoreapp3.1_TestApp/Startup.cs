using System;
using System.Net;
using System.Threading.Tasks;
using AspNetCore_netcoreapp3._1_TestApp.Authentication;
using AspNetCore_netcoreapp3._1_TestApp.Endpoints;
using AspNetCore_netcoreapp3_1_TestApp.Middleware;
using AspNetCore_netcoreapp3_1_TestApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using tusdotnet;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddSingleton(CreateTusConfigurationForCleanupService());
            services.AddHostedService<ExpiredFilesCleanupService>();
            services.AddSingleton(GetTusConfigurationFactory());

            services.AddAuthentication("BasicAuthentication")
                    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.Use((context, next) =>
            {
                // Default limit was changed some time ago. Should work by setting MaxRequestBodySize to null using ConfigureKestrel but this does not seem to work for IISExpress.
                // Source: https://github.com/aspnet/Announcements/issues/267
                context.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = null;
                return next.Invoke();
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSimpleExceptionHandler();

            app.UseAuthentication();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseHttpsRedirection();

            app.UseCors(builder => builder
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowAnyOrigin()
               .WithExposedHeaders(CorsHelper.GetExposedHeaders()));

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/files/{fileId}", DownloadFileEndpoint.HandleRoute);

                // This will run tus on the /files endpoint and look for the configuration in the IOC container.
                // Load order from IOC:
                // 1. Func<HttpContext, Task<DefaultTusConfiguration>>
                // 2. DefaultTusConfiguration
                endpoints.MapTus("/files");

                /* Alternatively you can provide the configuration for this specific endpoint:

                    endpoints.MapTus("/files/", new DefaultTusConfiguration
                    {
                        // Omitted
                    });

                */

                /* Or use a factory:

                    app.MapTus("/files/", async httpContext => new DefaultTusConfiguration
                    {
                        // ...
                    });

                */
            });
        }

        private static DefaultTusConfiguration CreateTusConfigurationForCleanupService()
        {
            // Simplified configuration just for the ExpiredFilesCleanupService to show load order of configs.
            return new DefaultTusConfiguration
            {
                Store = new TusDiskStore(@"C:\tusfiles\"),
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(5))
            };
        }

        private Func<HttpContext, Task<DefaultTusConfiguration>> GetTusConfigurationFactory()
        {
            return TusConfigurationFactory;
        }

        private Task<DefaultTusConfiguration> TusConfigurationFactory(HttpContext httpContext)
        {
            var logger = httpContext.RequestServices.GetService<ILoggerFactory>().CreateLogger<Startup>();

            // Change the value of EnableOnAuthorize in appsettings.json to enable or disable
            // the new authorization event.
            var enableAuthorize = Configuration.GetValue<bool>("EnableOnAuthorize");

            var config = new DefaultTusConfiguration
            {
                Store = new TusDiskStore(@"C:\tusfiles\"),
                MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
                UsePipelinesIfAvailable = true,
                Events = new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        if (!enableAuthorize)
                            return Task.CompletedTask;

                        if (!ctx.HttpContext.User.Identity.IsAuthenticated)
                        {
                            ctx.HttpContext.Response.Headers.Add("WWW-Authenticate", new StringValues("Basic realm=tusdotnet-test-netcoreapp2.2"));
                            ctx.FailRequest(HttpStatusCode.Unauthorized);
                            return Task.CompletedTask;
                        }

                        if (ctx.HttpContext.User.Identity.Name != "test")
                        {
                            ctx.FailRequest(HttpStatusCode.Forbidden, "'test' is the only allowed user");
                            return Task.CompletedTask;
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

                        return Task.CompletedTask;
                    },

                    OnBeforeCreateAsync = ctx =>
                    {
                        // Partial files are not complete so we do not need to validate
                        // the metadata in our example.
                        if (ctx.FileConcatenation is FileConcatPartial)
                        {
                            return Task.CompletedTask;
                        }

                        if (!ctx.Metadata.ContainsKey("name") || ctx.Metadata["name"].HasEmptyValue)
                        {
                            ctx.FailRequest("name metadata must be specified. ");
                        }

                        if (!ctx.Metadata.ContainsKey("contentType") || ctx.Metadata["contentType"].HasEmptyValue)
                        {
                            ctx.FailRequest("contentType metadata must be specified. ");
                        }

                        return Task.CompletedTask;
                    },
                    OnCreateCompleteAsync = ctx =>
                    {
                        logger.LogInformation($"Created file {ctx.FileId} using {ctx.Store.GetType().FullName}");
                        return Task.CompletedTask;
                    },
                    OnBeforeDeleteAsync = ctx =>
                    {
                        // Can the file be deleted? If not call ctx.FailRequest(<message>);
                        return Task.CompletedTask;
                    },
                    OnDeleteCompleteAsync = ctx =>
                    {
                        logger.LogInformation($"Deleted file {ctx.FileId} using {ctx.Store.GetType().FullName}");
                        return Task.CompletedTask;
                    },
                    OnFileCompleteAsync = ctx =>
                    {
                        logger.LogInformation($"Upload of {ctx.FileId} completed using {ctx.Store.GetType().FullName}");
                        // If the store implements ITusReadableStore one could access the completed file here.
                        // The default TusDiskStore implements this interface:
                        //var file = await ctx.GetFileAsync();
                        return Task.CompletedTask;
                    }
                },
                // Set an expiration time where incomplete files can no longer be updated.
                // This value can either be absolute or sliding.
                // Absolute expiration will be saved per file on create
                // Sliding expiration will be saved per file on create and updated on each patch/update.
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(5))
            };

            return Task.FromResult(config);
        }
    }
}
