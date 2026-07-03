using System.IO;
using System.Threading.Tasks;

namespace tusdotnet.Extensions
{
    // TODO should probably not be public, but is used in tusdotnet.ExternalMiddleware.Core.TusResponseWriter
    public static class TusHandleRequestResultExtensions
    {
        public static Task WriteMessageToStream(
            this TusHandleRequestResult result,
            Stream clientResponseStream
        ) => MessageStreamWriter.WriteAsync(result.Message, clientResponseStream);
    }
}
