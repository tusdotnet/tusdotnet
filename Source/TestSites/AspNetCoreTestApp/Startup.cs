using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using tusdotnet;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Stores;

namespace AspNetCoreTestApp
{
	public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors(builder => builder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                .WithExposedHeaders(CorsHelper.GetExposedHeaders()));

			app.UseDefaultFiles();
			app.UseStaticFiles();

			app.UseTus(context => new DefaultTusConfiguration
			{
				UrlPath = "/files",
				Store = new TusDiskStore(@"C:\tusfiles\"),
				OnUploadCompleteAsync = (fileId, store, cancellationToken) =>
				{
					Debug.WriteLine($"Upload of {fileId} completed using {store.GetType().FullName}");
					return Task.CompletedTask;
				}
			});
		}
    }
}
