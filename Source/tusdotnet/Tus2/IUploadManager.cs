using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal interface IUploadManager
    {
        Task CancelOtherUploads(string uploadToken);
        Task FinishUpload(string uploadToken);
        Task NotifyCancelComplete(string uploadToken);
        Task<CancellationToken> StartUpload(string uploadToken);
    }
}