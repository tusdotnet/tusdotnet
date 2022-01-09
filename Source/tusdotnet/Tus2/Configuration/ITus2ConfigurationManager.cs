using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal interface ITus2ConfigurationManager
    {
        Task<ITus2Store> GetStore();
        Task<ITus2Store> GetStore(string name);
        Task<IUploadManager> GetUploadManager();
        Task<IUploadManager> GetUploadManager(string name);
    }
}