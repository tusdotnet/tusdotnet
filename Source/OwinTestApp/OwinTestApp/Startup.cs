using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using OwinTestApp;
using tusdotnet;
using tusdotnet.Models;

[assembly: OwinStartup(typeof(Startup))]

namespace OwinTestApp
{
	public class Startup
	{
		public void Configuration(IAppBuilder app)
		{
			app.Use(async (context, next) =>
			{
				try
				{
					await next.Invoke();
				}
				catch (Exception exc)
				{
					Console.Error.WriteLine(exc.ToString());
				}
			});

			app.UseTus(() => new DefaultTusConfiguration
			{
				Store = new TusDiskStore(@"C:\tusfiles\"),
				UrlPath = "/files"
			});

			app.Use((context, next) =>
			{
				switch (context.Request.Uri.LocalPath)
				{
					case "/":
						context.Response.ContentType = "text/html";
						return context.Response.WriteAsync(File.ReadAllText("../../upload.html"));
					case "/tus.js":
						context.Response.ContentType = "application/js";
						return context.Response.WriteAsync(File.ReadAllText("../../tus.js"));
					default:
						context.Response.StatusCode = 404;
						return Task.FromResult(false);

				}


			});
		}
	}
}
