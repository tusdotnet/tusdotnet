using System.Net;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Extensions
{
    internal static class ResponseAdapterExtensions
    {
        private static readonly Encoding _utf8Encoding = new UTF8Encoding();

#warning TODO No need to return bool as the return value is no longer used
        internal static bool NotFound(this ResponseAdapter response)
        {
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
            response.SetStatus(HttpStatusCode.NotFound);
            return true;
        }

#warning TODO No need to return bool as the return value is no longer used
        internal static async Task<bool> Error(this ResponseAdapter response, HttpStatusCode statusCode, string message, bool includeTusResumableHeader = true)
        {
            if (includeTusResumableHeader)
            {
                response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            }

            response.SetStatus(statusCode);
            if (message != null)
            {
                response.SetHeader(HeaderConstants.ContentType, "text/plain");
                var buffer = _utf8Encoding.GetBytes(message);
                await response.Body.WriteAsync(buffer, 0, buffer.Length);
            }
            return true;
        }
    }
}
