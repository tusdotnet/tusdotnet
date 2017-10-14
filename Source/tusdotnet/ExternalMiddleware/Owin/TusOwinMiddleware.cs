#if netfull

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Owin;
using tusdotnet.Adapters;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    /// <summary>
    /// Processes tus.io requests for OWIN.
    /// </summary>
    public class TusOwinMiddleware : OwinMiddleware
	{
		private readonly Func<IOwinRequest, Task<DefaultTusConfiguration>> _configFactory;

	    /// <summary>Creates a new instance of TusOwinMiddleware.</summary>
	    /// <param name="next"></param>
	    /// <param name="configFactory"></param>
        public TusOwinMiddleware(OwinMiddleware next, Func<IOwinRequest, Task<DefaultTusConfiguration>> configFactory) : base(next)
		{
			_configFactory = configFactory;
		}

	    /// <summary>
	    /// Handles the tus.io request.
	    /// </summary>
	    /// <param name="context">The IOwinContext</param>
	    /// <returns></returns>
        public override async Task Invoke(IOwinContext context)
		{
			var request = new RequestAdapter
			{
				Headers = context.Request.Headers.ToDictionary(f => f.Key, f => f.Value.ToList(), StringComparer.OrdinalIgnoreCase),
				Body = context.Request.Body,
				Method = context.Request.Method,
				RequestUri = context.Request.Uri
			};

			var response = new ResponseAdapter
			{
				Body = context.Response.Body,
				SetHeader = (key, value) => context.Response.Headers[key] = value,
				SetStatus = status => context.Response.StatusCode = status
			};
			
			var config = await _configFactory(context.Request);

			var handled = await TusProtocolHandler.Invoke(new ContextAdapter
			{
				Request = request,
				Response = response,
				Configuration = config,
				CancellationToken = context.Request.CallCancelled
			});

			if (!handled)
			{
				await Next.Invoke(context);
			}
		}
	}
}

#endif