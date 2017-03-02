#if netfull

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Owin;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
	public class TusOwinMiddleware : OwinMiddleware
	{
		private readonly Func<IOwinRequest, ITusConfiguration> _configFactory;

		public TusOwinMiddleware(OwinMiddleware next, Func<IOwinRequest, ITusConfiguration> configFactory) : base(next)
		{
			_configFactory = configFactory;
		}

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
			
			var config = _configFactory(context.Request);

			var handled = await TusMiddleware.Invoke(new ContextAdapter
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