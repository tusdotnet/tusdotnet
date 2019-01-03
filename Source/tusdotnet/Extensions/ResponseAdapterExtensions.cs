using System.Net;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;

namespace tusdotnet.Extensions
{
    internal static class ResponseAdapterExtensions
    {
#warning TODO No need to return bool as the return value is no longer used
        internal static bool NotFound(this ResponseAdapter response)
        {
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
            response.SetStatus((int)HttpStatusCode.NotFound);
            return true;
        }

#warning TODO No need to return bool as the return value is no longer used
        internal static async Task<bool> Error(this ResponseAdapter response, HttpStatusCode statusCode, string message)
        {
            response.SetHeader(HeaderConstants.ContentType, "text/plain");
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetStatus((int)statusCode);
            var buffer = new UTF8Encoding().GetBytes(message);
            await response.Body.WriteAsync(buffer, 0, buffer.Length);
            return true;
        }
    }
}
