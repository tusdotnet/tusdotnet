using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public interface ITus2StorageFactory
    {
        Task<Tus2StorageFacade> CreateDefaultStorage();

        Task<Tus2StorageFacade> CreateNamedStorage(string storageName);
    }
}
