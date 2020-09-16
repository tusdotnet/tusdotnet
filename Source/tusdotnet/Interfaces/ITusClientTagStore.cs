using System;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Interfaces
{
    public interface ITusClientTagStore
    {
        Task SetClientTagAsync(string fileId, string uploadTag, string user);

        Task<ClientTagFileIdMap> ResolveUploadTagToFileIdAsync(string uploadTag);
    }
}
