using System.IO;
using System.Threading.Tasks;

namespace tusdotnet.Extensions
{
    internal static class TusHandleRequestResultExtensions
    {
        internal static Task WriteMessageToStream(
            this TusHandleRequestResult result,
            Stream clientResponseStream
        ) => MessageStreamWriter.WriteAsync(result.Message, clientResponseStream);
    }
}
