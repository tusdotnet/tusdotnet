using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Interfaces
{
    public interface ITusChallengeStore
    {
        Task SetUploadSecretAsync(string fileId, string uploadSecret, CancellationToken cancellationToken);

        Task<string> GetUploadSecretAsync(string fileId, CancellationToken cancellationToken);
    }
}
