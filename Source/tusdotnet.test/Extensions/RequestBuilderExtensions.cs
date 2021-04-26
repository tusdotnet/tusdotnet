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

		internal static RequestBuilder AddHeaderIfNotEmpty(this RequestBuilder builder, string name, string value)
		{
			return string.IsNullOrEmpty(value) ? builder : builder.AddHeader(name, value);
		}

		internal static RequestBuilder CreateTusResumableRequest(this TestServer server, string path)
		{
			return server.CreateRequest(path).AddTusResumableHeader();
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

		
		internal static RequestBuilder AddBody(this RequestBuilder builder)
		{
			return builder.And(m => m.AddBody());
		}

		internal static RequestBuilder AddBody(this RequestBuilder builder, string contentType)
		{
			return builder.And(m => m.AddBody(contentType));
		}

		internal static RequestBuilder AddBody(this RequestBuilder builder, int size)
		{
			return builder.And(m => m.AddBody(size));
		}

#endif

#if netstandard

		internal static RequestBuilder AddTusResumableHeader(this RequestBuilder builder)
		{
			return builder.AddHeader("Tus-Resumable", "1.0.0");
		}

		internal static RequestBuilder AddHeaderIfNotEmpty(this RequestBuilder builder, string name, string value)
        {
            return string.IsNullOrEmpty(value) ? builder : builder.AddHeader(name, value);
        }

        internal static RequestBuilder CreateTusResumableRequest(this TestServer server, string path)
		{
			return server.CreateRequest(path).AddTusResumableHeader();
		}

		internal static RequestBuilder AddBody(this RequestBuilder builder)
		{
			return builder.And(m => m.AddBody());
		}

		internal static RequestBuilder AddBody(this RequestBuilder builder, string contentType)
		{
			return builder.And(m => m.AddBody(contentType));
		}

		internal static RequestBuilder AddBody(this RequestBuilder builder, int size)
		{
			return builder.And(m => m.AddBody(size));
		}

		/// <summary>
		/// Add a X-Http-Method-Override to the builder if method and override does not match.
		/// Otherwise just return the builder as is.
		/// </summary>
		/// <param name="builder">The builder</param>
		/// <param name="override">The http method to add as X-Http-Method-Override</param>
		/// <param name="method">The real http method used</param>
		/// <returns>The builder with or without the X-Http-Method-Override</returns>
		internal static RequestBuilder OverrideHttpMethodIfNeeded(this RequestBuilder builder, string @override, string method)
		{
			return !method.Equals(@override, StringComparison.OrdinalIgnoreCase)
				? builder.AddHeader("X-Http-Method-Override", @override)
				: builder;
		}

	#if trailingheaders

		internal static RequestBuilder DeclareTrailingChecksumHeader(this RequestBuilder builder)
		{
			return builder.And(req => req.Headers.Trailer.Add("Upload-Checksum"));
		}

	#endif

#endif
	}
}
