using System.IO;
using tusdotnet.Models.Streams;

namespace tusdotnet.Extensions.Store
{
    public static class StreamExtensions
    {
        public static ChecksumInfo GetUploadChecksumInfo(this Stream stream)
        {
            return stream is ChecksumAwareStream checksumStream
                ? new() {  Algorithm = checksumStream.Checksum.Algorithm }
                : null;

        }
    }
}
