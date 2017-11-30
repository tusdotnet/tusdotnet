using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
	/// <summary>
	/// Processes tus.io requests for ASP.NET Core.
	/// </summary>
	public class TusCoreMiddleware
	{
		private readonly RequestDelegate _next;

		private readonly Func<HttpContext, Task<DefaultTusConfiguration>> _configFactory;

		/// <summary>Creates a new instance of TusCoreMiddleware.</summary>
		/// <param name="next"></param>
		/// <param name="configFactory"></param>
		public TusCoreMiddleware(RequestDelegate next, Func<HttpContext, Task<DefaultTusConfiguration>> configFactory)
		{
			_next = next;
			_configFactory = configFactory;
		}

		/// <summary>
		/// Handles the tus.io request.
		/// </summary>
		/// <param name="context">The HttpContext</param>
		/// <returns></returns>
		public async Task Invoke(HttpContext context)
		{
			var request = new RequestAdapter
				              {
					              Headers =
						              context.Request.Headers.ToDictionary(
							              f => f.Key,
							              f => f.Value.ToList(),
							              StringComparer.OrdinalIgnoreCase),
					              Body = context.Request.Body,
					              Method = context.Request.Method,
					              RequestUri = new Uri(
						              $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}")
				              };

			var response = new ResponseAdapter
				               {
					               Body = context.Response.Body,
					               SetHeader = (key, value) => context.Response.Headers[key] = value,
					               SetStatus = status => context.Response.StatusCode = status
				               };

			var config = await _configFactory(context);

		    var handled = await TusProtocolHandler.Invoke(new ContextAdapter
		    {
		        Request = request,
		        Response = response,
		        Configuration = config,
		        CancellationToken = context.RequestAborted
            });

			if (!handled)
			{
				await _next(context);
			}
		}
	}
}