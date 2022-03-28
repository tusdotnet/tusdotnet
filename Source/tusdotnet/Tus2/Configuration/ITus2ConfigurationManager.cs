using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public interface ITus2ConfigurationManager
    {
        Task<Tus2StorageFacade> GetDefaultStorage();
        
        Task<IOngoingUploadManager> GetDefaultUploadManager();
        
        Task<Tus2StorageFacade> GetNamedStorage(string name);
    }
}