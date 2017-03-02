using System;
#if netstandard
using Microsoft.AspNetCore.TestHost;
#endif
#if netfull
using Microsoft.Owin.Testing;
#endif

namespace tusdotnet.test.Extensions
{
	internal static class RequestBuilderExtensions
	{
#if netfull

	internal static RequestBuilder AddTusResumableHeader(this RequestBuilder builder)
		{
			return builder.AddHeader("Tus-Resumable", "1.0.0");
		}

		/// <summary>
		/// Add a X-Http-Method-Override to the builder if method and override does not match.
		/// Otherwise just return the builder as is.
		/// </summary>
		/// <param name="builder">The builder</param>
		/// <param name="method">The real http method used</param>
		/// <param name="override">The http method to add as X-Http-Method-Override</param>
		/// <returns>The builder with or without the X-Http-Method-Override</returns>
		internal static RequestBuilder OverrideHttpMethodIfNeeded(this RequestBuilder builder, string @override, string method)
		{
			return !method.Equals(@override, StringComparison.InvariantCultureIgnoreCase)
				? builder.AddHeader("X-Http-Method-Override", @override)
				: builder;
		}

#endif

#if netstandard

		internal static RequestBuilder AddTusResumableHeader(this RequestBuilder builder)
		{
			return builder.AddHeader("Tus-Resumable", "1.0.0");
		}

		/// <summary>
		/// Add a X-Http-Method-Override to the builder if method and override does not match.
		/// Otherwise just return the builder as is.
		/// </summary>
		/// <param name="builder">The builder</param>
		/// <param name="method">The real http method used</param>
		/// <param name="override">The http method to add as X-Http-Method-Override</param>
		/// <returns>The builder with or without the X-Http-Method-Override</returns>
		internal static RequestBuilder OverrideHttpMethodIfNeeded(this RequestBuilder builder, string @override, string method)
		{
			return !method.Equals(@override, StringComparison.OrdinalIgnoreCase)
				? builder.AddHeader("X-Http-Method-Override", @override)
				: builder;
		}

#endif

	}
}
