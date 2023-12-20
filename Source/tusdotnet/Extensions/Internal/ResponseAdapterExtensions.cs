using System.Buffers;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Extensions
{
    internal static class ResponseAdapterExtensions
    {
        private static readonly Encoding _utf8Encoding = new UTF8Encoding(false);

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
            response.Error(RESOURCE_LOCKED, "File is currently being updated. Please try again later");
        }

        internal static void Error(this ResponseAdapter response, HttpStatusCode statusCode, string message, bool includeTusResumableHeader = true)
        {
            if (includeTusResumableHeader)
            {
                response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            }

            response.SetResponse(statusCode, message);
        }

        internal static async Task WriteMessageToStream(this ResponseAdapter response, Stream clientResponseStream)
        {
            // TODO: Use predefined byte arrays for known messages to reduce GC pressure.

            var bytes = ArrayPool<byte>.Shared.Rent(_utf8Encoding.GetByteCount(response.Message));

            try
            {
                var byteCount = _utf8Encoding.GetBytes(response.Message, 0, response.Message.Length, bytes, 0);

#if NETCOREAPP3_1_OR_GREATER

#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync' - Ignore as it's not supported on netcoreapp3.1 and we write the entire array anyway.

                await clientResponseStream.WriteAsync(bytes, 0, byteCount);

#pragma warning restore CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

#else
                using var ms = new MemoryStream(bytes, 0, byteCount);
                await ms.CopyToAsync(clientResponseStream);
#endif
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
    }
}
