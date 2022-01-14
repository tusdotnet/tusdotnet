using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal interface ITus2ConfigurationManager
    {
        Task<ITus2Storage> GetDefaultStorage();
        
        Task<IOngoingUploadManager> GetDefaultUploadManager();
        
        Task<ITus2Storage> GetNamedStorage(string name);

        Task<IOngoingUploadManager> GetNamedUploadManager(string name);
    }
}