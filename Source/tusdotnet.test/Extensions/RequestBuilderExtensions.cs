using Microsoft.Owin.Testing;

namespace tusdotnet.test.Extensions
{
	internal static class RequestBuilderExtensions
	{
		internal static RequestBuilder AddTusResumableHeader(this RequestBuilder builder)
		{
			return builder.AddHeader("Tus-Resumable", "1.0.0");
		}
	}
}
