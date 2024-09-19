#if pipelines

using System.IO.Pipelines;
using tusdotnet.Models.PipeReaders;

namespace tusdotnet.Extensions.Store
{
    public static class PipeReaderExtensions
    {
        public static ChecksumInfo GetUploadChecksumInfo(this PipeReader pipeReader)
        {
            return pipeReader is ChecksumAwarePipeReader checksumAwarePipeReader
                ? new() { Algorithm = checksumAwarePipeReader.Checksum.Algorithm }
                : null;
        }
    }
}

#endif