using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using OwinTestApp;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Stores;

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
					Console.Error.WriteLine(exc);
				}
			});

			app.UseTus(() => new DefaultTusConfiguration
			{
				Store = new TusDiskStore(@"C:\tusfiles\"),
				UrlPath = "/files",
				OnUploadCompleteAsync = (fileId, store, cancellationToken) =>
				{
					Console.WriteLine($"Upload of {fileId} is complete. Callback also got a store of type {store.GetType().FullName}");
					// If the store implements ITusReadableStore one could access the completed file here.
					// The default TusDiskStore implements this interface:
					// var file = await (store as ITusReadableStore).GetFileAsync(fileId, cancellationToken);
					return Task.FromResult(true);
				}
			});

			app.Use(async (context, next) =>
			{
				// All GET requests to tusdotnet are forwared so that you can handle file downloads.
				// This is done because the file's metadata is domain specific and thus cannot be handled 
				// in a generic way by tusdotnet.
				if (context.Request.Uri.LocalPath.StartsWith("/files/", StringComparison.Ordinal))
				{
					var fileId = context.Request.Uri.LocalPath.Replace("/files/", "").Trim();
					if (!string.IsNullOrEmpty(fileId))
					{
						var store = new TusDiskStore(@"C:\tusfiles\");
						var file = await store.GetFileAsync(fileId, context.Request.CallCancelled);

						if (file == null)
						{
							context.Response.StatusCode = 404;
							await context.Response.WriteAsync($"File with id {fileId} was not found.", context.Request.CallCancelled);
							return;
						}

						var fileStream = await file.GetContent(context.Request.CallCancelled);

						context.Response.ContentType = "application/octet-stream";
						await fileStream.CopyToAsync(context.Response.Body, 81920, context.Request.CallCancelled);
						return;
					}
				}

				switch (context.Request.Uri.LocalPath)
				{
					case "/":
						context.Response.ContentType = "text/html";
						await context.Response.WriteAsync(File.ReadAllText("../../upload.html"), context.Request.CallCancelled);
						break;
					case "/tus.js":
						context.Response.ContentType = "application/js";
						await context.Response.WriteAsync(File.ReadAllText("../../tus.js"), context.Request.CallCancelled);
						break;
					default:
						context.Response.StatusCode = 404;
						break;
				}
			});
		}
	}
}
