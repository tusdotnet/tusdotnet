using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal interface ITus2ConfigurationManager
    {
        Task<ITus2Storage> GetDefaultStorage();
        
        Task<IUploadManager> GetDefaultUploadManager();
        
        Task<ITus2Storage> GetNamedStorage(string name);

        Task<IUploadManager> GetNamedUploadManager(string name);
    }
}