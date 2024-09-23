using System.IO;
using tusdotnet.Models.Streams;

namespace tusdotnet.Extensions.Store
{
    /// <summary>
    /// Extension methods for <see cref="Stream"/> to integrate with features in tusdotnet.
    /// Methods in this class are designed to be ran from <see cref="Interfaces.ITusStore.AppendDataAsync(string, Stream, System.Threading.CancellationToken)"/>
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Returns information about the Upload-Checksum header provided by the client.
        /// If the client did not provide the header, or if the store does not implement <see cref="Interfaces.ITusChecksumStore"/>, this method will return null.
        /// </summary>
        /// <param name="stream">The Stream provided to AppendDataAsync</param>
        /// <returns>Information about the Upload-Checksum header or null</returns>
        public static ChecksumInfo GetUploadChecksumInfo(this Stream stream)
        {
            return stream is ChecksumAwareStream checksumStream
                ? new() {  Algorithm = checksumStream.Checksum.Algorithm }
                : null;

        }
    }
}
