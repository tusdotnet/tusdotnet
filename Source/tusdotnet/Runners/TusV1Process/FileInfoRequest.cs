#if NET6_0_OR_GREATER
using tusdotnet.Adapters;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public class FileInfoRequest : TusV1Request
    {
        public string FileId { get; set; }

        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            return ToContextAdapter("head", config, fileId: FileId);
        }

        internal static FileInfoRequest FromContextAdapter(ContextAdapter contextAdapter)
        {
            return new()
            {
                CancellationToken = contextAdapter.CancellationToken,
                FileId = contextAdapter.FileId
            };
        }
    }
}
#endif