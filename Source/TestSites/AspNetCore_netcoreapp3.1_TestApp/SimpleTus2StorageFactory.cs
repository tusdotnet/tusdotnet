using System.Threading.Tasks;
using tusdotnet.Tus2;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class SimpleTus2StorageFactory : ITus2StorageFactory
    {
        public async Task<Tus2StorageFacade> CreateDefaultStorage()
        {
            return new Tus2StorageFacade(new Tus2DiskStorage(new() { DiskPath = @"C:\tusfiles" }));
        }

        public async Task<Tus2StorageFacade> CreateNamedStorage(string storageName)
        {
            var path = @"C:\tusfiles\" + storageName;
            if(!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);

            return new Tus2StorageFacade(new Tus2DiskStorage(new() { DiskPath = @"C:\tusfiles\" + storageName }));
        }
    }
}
