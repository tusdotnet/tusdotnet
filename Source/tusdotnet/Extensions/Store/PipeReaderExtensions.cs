#if pipelines

using System.IO.Pipelines;
using tusdotnet.Models.PipeReaders;

namespace tusdotnet.Extensions.Store
{
    /// <summary>
    /// Extension methods for <see cref="PipeReader"/> to integrate with features in tusdotnet.
    /// Methods in this class are designed to be ran from <see cref="Interfaces.ITusPipelineStore.AppendDataAsync(string, PipeReader, System.Threading.CancellationToken)"/>
    /// </summary>
    public static class PipeReaderExtensions
    {
        /// <summary>
        /// Returns information about the Upload-Checksum header provided by the client.
        /// If the client did not provide the header, or if the store does not implement <see cref="Interfaces.ITusChecksumStore"/>, this method will return null.
        /// </summary>
        /// <param name="pipeReader">The PipeReader provided to AppendDataAsync</param>
        /// <returns>Information about the Upload-Checksum header or null</returns>
        public static ChecksumInfo GetUploadChecksumInfo(this PipeReader pipeReader)
        {
            return pipeReader is ChecksumAwarePipeReader checksumAwarePipeReader
                ? new() { Algorithm = checksumAwarePipeReader.Checksum.Algorithm }
                : null;
        }
    }
}

#endif