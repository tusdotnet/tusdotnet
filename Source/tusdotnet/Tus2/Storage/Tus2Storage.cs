using System.Threading.Tasks;
using tusdotnet.Tus2.Store;

namespace tusdotnet.Tus2
{
    public abstract class Tus2Storage
    {
        public virtual Task WriteData(string uploadToken, WriteDataContext context)
        {
            Tus2StorageThrowHelper.ThrowNotImplemented();
            return Task.CompletedTask;
        }

        public virtual Task CreateFile(string uploadToken, CreateFileContext context)
        {
            Tus2StorageThrowHelper.ThrowNotImplemented();
            return Task.CompletedTask;
        }

        public virtual Task Delete(string uploadToken)
        {
            Tus2StorageThrowHelper.ThrowNotImplemented();
            return Task.CompletedTask;
        }

        // TODO: Replace FileExist, GetOffset and IsComplete with something like "GetFileInfo"
        // so that we can grab these in a single call to the storage implementation.
        public virtual Task<bool> FileExist(string uploadToken)
        {
            Tus2StorageThrowHelper.ThrowNotImplemented();
            return Task.FromResult(false);
        }

        public virtual Task<long> GetOffset(string uploadToken)
        {
            Tus2StorageThrowHelper.ThrowNotImplemented();
            return Task.FromResult(0L);
        }

        public virtual Task<bool> IsComplete(string uploadToken)
        {
            Tus2StorageThrowHelper.ThrowNotImplemented();
            return Task.FromResult(false);
        }

        public virtual Task MarkComplete(string uploadToken)
        {
            Tus2StorageThrowHelper.ThrowNotImplemented();
            return Task.CompletedTask;
        }
    }
}