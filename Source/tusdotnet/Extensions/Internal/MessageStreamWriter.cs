using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Extensions
{
    internal static class MessageStreamWriter
    {
        // TODO: Use predefined byte arrays for known messages to reduce GC pressure.
        private static readonly Encoding _utf8Encoding = new UTF8Encoding(false);

        internal static async Task WriteAsync(string message, Stream clientResponseStream)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(_utf8Encoding.GetByteCount(message));

            try
            {
                var byteCount = _utf8Encoding.GetBytes(message, 0, message.Length, bytes, 0);

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
