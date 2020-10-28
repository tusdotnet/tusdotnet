using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Extensions.Internal
{
    internal static class RequestAdapterExtensions
    {
        /// <summary>
        /// Returns the request method taking X-Http-Method-Override into account.
        /// </summary>
        /// <param name="request">The request to get the method for</param>
        /// <returns>The request method</returns>
        internal static string GetHttpMethod(this RequestAdapter request)
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
