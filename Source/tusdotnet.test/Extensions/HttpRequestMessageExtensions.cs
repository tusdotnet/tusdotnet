using System.Net.Http;
using System.Net.Http.Headers;

namespace tusdotnet.test.Extensions
{
    internal static class HttpRequestMessageExtensions
    {
        private const int DEFAULT_SIZE = 3;

        internal static void AddBody(this HttpRequestMessage message)
        {
            AddBody(message, size: DEFAULT_SIZE);
        }

        internal static void AddBody(this HttpRequestMessage message, int size)
        {
            AddBody(message, "application/offset+octet-stream", size);
        }

        internal static void AddBody(this HttpRequestMessage message, string contentType)
        {
            AddBody(message, contentType, DEFAULT_SIZE);
        }

        private static void AddBody(this HttpRequestMessage message, string contentType, int size)
        {
            message.Content = new ByteArrayContent(new byte[size]);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
    }
}
