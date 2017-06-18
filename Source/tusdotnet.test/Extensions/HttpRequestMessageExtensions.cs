using System.Net.Http;
using System.Net.Http.Headers;

namespace tusdotnet.test.Extensions
{
    internal static class HttpRequestMessageExtensions
    {
        internal static void AddBody(this HttpRequestMessage message)
        {
            AddBody(message, "application/offset+octet-stream");
        }

        internal static void AddBody(this HttpRequestMessage message, string contentType)
        {
            message.Content = new ByteArrayContent(new byte[] { 0, 0, 0 });
            message.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
    }
}
