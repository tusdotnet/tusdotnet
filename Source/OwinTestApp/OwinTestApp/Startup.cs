using System.IO;
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
			app.UseTus(new DefaultTusConfiguration
			{
				Store = new TusDiskStore(),
				UrlPath = "/files"
			});

			app.Use((context, next) =>
			{
				context.Response.ContentType = "text/html";
				return context.Response.WriteAsync(File.ReadAllText("../../upload.html"));
			});
		}
	}
}
