using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public interface ITus2Storage
    {
        Task WriteData(string uploadToken, WriteDataContext context);

        Task CreateFile(string uploadToken, CreateFileContext context);

        Task Delete(string uploadToken);

        Task<bool> FileExist(string uploadToken);

        Task<long> GetOffset(string uploadToken);

        Task<bool> IsComplete(string uploadToken);

        Task MarkComplete(string uploadToken);
    }
}