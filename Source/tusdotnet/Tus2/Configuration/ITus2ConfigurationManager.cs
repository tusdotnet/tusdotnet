using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public interface ITus2ConfigurationManager
    {
        Task<Tus2Storage> GetDefaultStorage();
        
        Task<IOngoingUploadManager> GetDefaultUploadManager();
        
        Task<Tus2Storage> GetNamedStorage(string name);

        Task<IOngoingUploadManager> GetNamedUploadManager(string name);
    }
}