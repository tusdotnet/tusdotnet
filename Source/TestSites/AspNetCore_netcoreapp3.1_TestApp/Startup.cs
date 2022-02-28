using System;
using System.Buffers;
using System.Security.Cryptography;
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
using tusdotnet;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;
using tusdotnet.Tus2;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static IConfiguration Configuration { get; private set; }

        public static string DirectoryPath => Configuration.GetValue<string>("FolderDiskPath");

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddSingleton(CreateTusConfiguration);
            services.AddHostedService<ExpiredFilesCleanupService>();

            services.AddAuthentication("BasicAuthentication")
                    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

            services.AddOptions();

            var tus2Configuration = new Tus2Options();
            Configuration.Bind(tus2Configuration);

            services.AddTus2(options =>
            {
                options.AddDiskStorage("MyProfile", tus2Configuration.FolderDiskPath);
                //options.AddDiskBasedUploadManager("MyProfile", @"C:\tusfiles");
                options.AddStorage(async httpContext => new Tus2DiskStorage(new()
                {
                    DiskPath = System.IO.Path.Combine(tus2Configuration.FolderDiskPath, httpContext.User.Identity.Name)
                }));
                //options.AddUploadManager(new OngoingUploadManagerDiskBased(new() { SharedDiskPath = System.IO.Path.GetTempPath() }));
                options.AddHandler<MyTusHandler>();
            });
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

            //app.UseDefaultFiles();
            //app.UseStaticFiles();

            //app.UseHttpsRedirection();

            app.UseCors(builder => builder
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowAnyOrigin()
               .WithExposedHeaders(CorsHelper.GetExposedHeaders()));

            // httpContext parameter can be used to create a tus configuration based on current user, domain, host, port or whatever.
            // In this case we just return the same configuration for everyone.
            app.UseTus(httpContext => Task.FromResult(httpContext.RequestServices.GetService<DefaultTusConfiguration>()));

            app.UseRouting();

            // All GET requests to tusdotnet are forwarded so that you can handle file downloads.
            // This is done because the file's metadata is domain specific and thus cannot be handled 
            // in a generic way by tusdotnet.
            app.UseEndpoints(endpoints =>
            {
                var filesTus2Config = new EndpointConfiguration("MyProfile")
                {
                    AllowClientToDeleteFile = true
                };

                endpoints.MapTus2<MyTusHandler>("/files-tus-2", filesTus2Config);

                endpoints.MapGet("/files/{fileId}", DownloadFileEndpoint.HandleRoute);
                endpoints.Map("/files-tus-2-info", Tus2InfoEndpoint.Invoke);
                endpoints.MapGet("/random-file-id", async httpContext =>
                {
                    var arr = ArrayPool<byte>.Shared.Rent(32);
                    var span = arr.AsMemory()[..32];
                    RandomNumberGenerator.Fill(span.Span);

                    await httpContext.Response.WriteAsync(":" + Convert.ToBase64String(span.Span) + ":");

                    ArrayPool<byte>.Shared.Return(arr, clearArray: true);
                });
            });
        }

        private DefaultTusConfiguration CreateTusConfiguration(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Startup>();

            return new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = new TusDiskStore(DirectoryPath),
                MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(30)),
                //MaxAllowedUploadSizeInBytesLong = 1024 * 1024,
                Events = new Events
                {
                    OnAuthorizeAsync = ctx =>
                    {
                        //if (!ctx.HttpContext.User.Identity.IsAuthenticated)
                        //{
                        //    ctx.HttpContext.Response.Headers.Add("WWW-Authenticate", new StringValues("Basic realm=tusdotnet-test-netcoreapp3.1"));
                        //    ctx.FailRequest(HttpStatusCode.Unauthorized);
                        //    return Task.CompletedTask;
                        //}

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

                    OnCreateCompleteAsync = ctx =>
                    {
                        logger.LogInformation($"Created file {ctx.FileId} using {ctx.Store.GetType().FullName}");
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
                    },
                    OnResolveClientTagAsync = ctx =>
                    {
                        if (ctx.ClientTagBelongsToCurrentUser || ctx.RequestPassesChallenge)
                        {
                            ctx.Allow();
                        }

                        // Other custom logic goes here, e.g. allowing all users in a group access or similar.

                        return Task.CompletedTask;
                    }
                }
            };
        }
    }
}
