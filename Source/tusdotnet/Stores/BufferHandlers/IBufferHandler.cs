using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Stores.BufferHandlers
{
    public interface IBufferHandler
    {
        Task<BufferHandlerCopyResult> CopyToFile(TusDiskBufferSize bufferSize, long fileUploadLengthProvidedDuringCreate, Stream stream, string fileLocation, CancellationToken cancellationToken);
    }
}