#if netstandard

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using tusdotnet.Interfaces;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    public static class TusApplicationBuilderExtensions
    {
	    public static IApplicationBuilder UseTus(this IApplicationBuilder appBuilder,
		    Func<HttpContext, ITusConfiguration> configFactory)
	    {
		    return appBuilder.UseMiddleware<TusCoreMiddleware>(configFactory);
	    }
	}
}

#endif