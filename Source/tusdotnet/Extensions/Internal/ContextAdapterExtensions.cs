using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.FileLocks;
using tusdotnet.Interfaces;

namespace tusdotnet.Extensions.Internal
{
    internal static class ContextAdapterExtensions
    {
        internal static Task<ITusFileLock> GetFileLock(this ContextAdapter context)
        {
            var lockProvider = GetLockProvider(context);
            return lockProvider.AquireLock(context.Request.FileId);
        }

        private static ITusFileLockProvider GetLockProvider(ContextAdapter context)
        {
            return context.Configuration.FileLockProvider ?? InMemoryFileLockProvider.Instance;
        }
    }
}
