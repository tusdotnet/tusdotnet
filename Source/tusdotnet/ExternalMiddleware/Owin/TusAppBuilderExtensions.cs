#if netfull

using System;
using Microsoft.Owin;
using Owin;
using tusdotnet.Interfaces;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
	public static class TusAppBuilderExtensions
	{
		public static void UseTus(this IAppBuilder builder, Func<IOwinRequest, ITusConfiguration> configFactory)
		{
			builder.Use<TusOwinMiddleware>(configFactory);
		}
	}
}

#endif