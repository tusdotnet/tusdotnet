using System.IO;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Extensions
{
    internal static class ResponseAdapterExtensions
    {

        internal static void NotFound(this ResponseAdapter response)
        {
            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.CacheControl, HeaderConstants.NoStore);
            response.SetResponse(HttpStatusCode.NotFound);
        }

        internal static void Locked(this ResponseAdapter response)
        {
            // HttpStatusCode.Locked is not available on netstandard nor net452.
            const HttpStatusCode RESOURCE_LOCKED = (HttpStatusCode)423;
            response.Error(
                RESOURCE_LOCKED,
                "File is currently being updated. Please try again later"
            );
        }

        internal static void Error(
            this ResponseAdapter response,
            HttpStatusCode statusCode,
            string message,
            bool includeTusResumableHeader = true
        )
        {
            if (includeTusResumableHeader)
            {
                response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            }

            response.SetResponse(statusCode, message);
        }

        internal static Task WriteMessageToStream(
            this ResponseAdapter response,
            Stream clientResponseStream
        ) => MessageStreamWriter.WriteAsync(response.Message, clientResponseStream);
    }
}
