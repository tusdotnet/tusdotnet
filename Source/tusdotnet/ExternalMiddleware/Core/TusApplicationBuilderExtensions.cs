#if netstandard

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
	/// <summary>
	/// Extension methods for adding tusdotnet to an application pipeline.
	/// </summary>
	public static class TusApplicationBuilderExtensions
	{
		/// <summary>
		/// Adds the tusdotnet middleware to your web application pipeline.
		/// </summary>
		/// <param name="appBuilder">The IApplicationBuilder passed to your configuration method</param>
		/// <param name="configFactory">Factory for creating the configuration for tusdotnet</param>
		public static IApplicationBuilder UseTus(
			this IApplicationBuilder appBuilder,
			Func<HttpContext, DefaultTusConfiguration> configFactory)
		{
			return appBuilder.UseMiddleware<TusCoreMiddleware>(configFactory);
		}
	}
}

#endif