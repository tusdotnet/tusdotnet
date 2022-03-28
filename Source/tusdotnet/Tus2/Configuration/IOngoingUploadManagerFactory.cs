using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public interface IOngoingUploadManagerFactory
    {
        Task<IOngoingUploadManager> CreateOngoingUploadManager();
    }
}
