using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;

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

        internal static void Error(this ResponseAdapter response, HttpStatusCode statusCode, string message, bool includeTusResumableHeader = true)
        {
            if (includeTusResumableHeader)
            {
                response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            }

            response.SetResponse(statusCode, message);
        }

#if NET6_0_OR_GREATER

        internal static string? GetResponseHeaderString(this ResponseAdapter response, string key, string? defaultValue = default)
        {
            return response.Headers.TryGetValue(key, out var value) ? value : defaultValue;
        }

        internal static long? GetResponseHeaderLong(this ResponseAdapter response, string key, long? defaultValue = default)
        {
            var str = response.GetResponseHeaderString(key, null);

            return str is not null && long.TryParse(str, out var value) ? value : defaultValue;
        }

        internal static IEnumerable<string> GetResponseHeaderList(this ResponseAdapter response, string key)
        {
            var list = response.GetResponseHeaderString(key);

            return list is not null
                ? list.Split(",", StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();
        }

        internal static DateTimeOffset? GetResponseHeaderDateTimeOffset(this ResponseAdapter response, string key)
        {
            var str = response.GetResponseHeaderString(key);
            return ExpirationHelper.ParseFromHeader(str);
        }

#endif

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
