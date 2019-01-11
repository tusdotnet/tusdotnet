using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Extensions
{
#warning TODO Move to IntentAnalyzer or merge with RequestAdapter to reduce times of parsing
    // ReSharper disable once InconsistentNaming
    internal static class RequestAdapterExtensions
    {
		/// <summary>
		/// Returns the request method taking X-Http-Method-Override into account.
		/// </summary>
		/// <param name="request">The request to get the method for</param>
		/// <returns>The request method</returns>
		internal static string GetMethod(this RequestAdapter request)
		{
			var method = request.GetHeader(HeaderConstants.XHttpMethodOveride);

			if (string.IsNullOrWhiteSpace(method))
			{
			    method = request.Method;
			}

			return method.ToLower();
		}
    }
}
