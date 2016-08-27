using System;
using Owin;
using tusdotnet.Interfaces;

namespace tusdotnet
{
	public static class TusAppBuilderExtensions
	{
		public static void UseTus(this IAppBuilder builder, Func<ITusConfiguration> configFactory)
		{
			builder.Use<TusMiddleware>(configFactory);
		}
	}
}
