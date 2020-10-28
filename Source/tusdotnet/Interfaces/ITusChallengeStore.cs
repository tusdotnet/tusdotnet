using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    public interface ITusChallengeStore
    {
        Task SetUploadSecretAsync(string fileId, string uploadSecret, CancellationToken cancellationToken);

        Task<string> GetUploadSecretAsync(string fileId, CancellationToken cancellationToken);

        Task<ITusChallengeStoreHashFunction> GetHashFunctionAsync(string algorithm, CancellationToken cancellationToken);

        //// TODO: Change signature to GetCheckSumAsync(fileid, algorithm, string[] parts...) to be able to change implementation in the future?
        //Task<byte[]> GetChecksumAsync(string fileId, string algorithm, string[] partsToHash, bool includeSecret, CancellationToken cancellationToken);

        Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken);
    }

    public interface ITusChallengeStoreHashFunction
    {
        byte[] CreateHash(string input);
    }
}
