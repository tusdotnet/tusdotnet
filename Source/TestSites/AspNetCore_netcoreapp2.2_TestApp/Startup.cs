using AspNetCore_netcoreapp2_2_TestApp.Authentication;
using AspNetCore_netcoreapp2_2_TestApp.Middleware;
using AspNetCore_netcoreapp2_2_TestApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

namespace AspNetCore_netcoreapp2_2_TestApp
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddSingleton(CreateTusConfiguration);
            services.AddHostedService<ExpiredFilesCleanupService>();
            services.AddAuthentication("BasicAuthentication")
                    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAuthentication();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseCors(builder => builder
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowAnyOrigin()
               .WithExposedHeaders(CorsHelper.GetExposedHeaders()));

            app.UseSimpleExceptionHandler();

            // httpContext parameter can be used to create a tus configuration based on current user, domain, host, port or whatever.
            // In this case we just return the same configuration for everyone.
            app.UseTus(httpContext => Task.FromResult(httpContext.RequestServices.GetService<DefaultTusConfiguration>()));

            // All GET requests to tusdotnet are forwarded so that you can handle file downloads.
            // This is done because the file's metadata is domain specific and thus cannot be handled 
            // in a generic way by tusdotnet.
            app.UseSimpleDownloadMiddleware(httpContext => Task.FromResult(httpContext.RequestServices.GetService<DefaultTusConfiguration>()));
        }

        private DefaultTusConfiguration CreateTusConfiguration(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            // Change the value of EnableOnAuthorize in appsettings.json to enable or disable
            // the new authorization event.
            var enableAuthorize = _configuration.GetValue<bool>("EnableOnAuthorize");

            return new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = new TusDiskStore(@"C:\tusfiles\"),
                MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
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
        }
    }
}
