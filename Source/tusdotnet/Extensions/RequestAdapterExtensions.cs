using System.Linq;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Extensions
{
	// ReSharper disable once InconsistentNaming
	internal static class IOwinRequestExtensions
	{
		/// <summary>
		/// Returns the request method taking X-Http-Method-Override into account.
		/// </summary>
		/// <param name="request">The request to get the method for</param>
		/// <returns>The request method</returns>
		internal static string GetMethod(this RequestAdapter request)
		{
			string method = null;

			if (request.Headers != null && request.Headers.ContainsKey(HeaderConstants.XHttpMethodOveride))
			{
				method = request.Headers[HeaderConstants.XHttpMethodOveride]?.FirstOrDefault()?.ToLower();
			}

			if (string.IsNullOrWhiteSpace(method))
			{
				method = request.Method.ToLower();
			}

			return method;
		}
	}
}
