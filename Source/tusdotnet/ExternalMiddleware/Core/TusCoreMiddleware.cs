#if netstandard

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    public class TusCoreMiddleware
    {
		private readonly RequestDelegate _next;
	    private readonly Func<HttpContext, ITusConfiguration> _configFactory;

		public TusCoreMiddleware(RequestDelegate next, Func<HttpContext, ITusConfiguration> configFactory)
		{
			_next = next;
			_configFactory = configFactory;
		}

		public async Task Invoke(HttpContext context)
		{
			var request = new RequestAdapter
			{
				Headers = context.Request.Headers.ToDictionary(f => f.Key, f => f.Value.ToList(), StringComparer.OrdinalIgnoreCase),
				Body = context.Request.Body,
				Method = context.Request.Method,
				RequestUri = new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}")
			};

			var response = new ResponseAdapter
			{
				Body = context.Response.Body,
				SetHeader = (key, value) => context.Response.Headers[key] = value,
				SetStatus = status => context.Response.StatusCode = status
			};

			var config = _configFactory(context);

			var handled = await TusProtocolHandler.Invoke(new ContextAdapter
			{
				Request =  request,
				Response =  response,
				CancellationToken = context.RequestAborted,
				Configuration = config,
			});


			if (!handled)
			{
				await _next(context);
			}
		}	    
	}
}

#endif