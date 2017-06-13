using System.Net;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Extensions
{
    internal static class ResponseAdapterExtensions
    {
        internal static bool NotFound(this ResponseAdapter response)
        {
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
            response.SetStatus((int)HttpStatusCode.NotFound);
            return true;
        }
    }
}
