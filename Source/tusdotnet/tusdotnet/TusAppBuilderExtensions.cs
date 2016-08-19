using Owin;
using tusdotnet.Interfaces;


namespace tusdotnet
{
	public static class TusAppBuilderExtensions
	{
		public static void UseTus(this IAppBuilder builder, ITusConfiguration config)
		{
			builder.Use<TusMiddleware>(config);
		}
	}
}
