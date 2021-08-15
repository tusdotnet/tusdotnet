#nullable enable

using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.FileLocks;
using tusdotnet.Interfaces;
#if trailingheaders
using Microsoft.AspNetCore.Http;
using tusdotnet.Constants;
using System.Linq;
#endif

namespace tusdotnet.Extensions.Internal
{
    internal static partial class ContextAdapterExtensions
    {
        internal static Task<ITusFileLock> GetFileLock(this ContextAdapter context)
        {
            var lockProvider = GetLockProvider(context);
            return lockProvider.AquireLock(context.Request.FileId);
        }

#if trailingheaders

        internal static string? GetTrailingUploadChecksumHeader(this ContextAdapter context)
        {
            var httpRequest = context.HttpContext.Request;

            if (!httpRequest.SupportsTrailers() || !httpRequest.CheckTrailersAvailable())
                return null;

            if (!context.HasDeclaredTrailingUploadChecksumHeader())
                return null;

            return httpRequest.GetTrailer(HeaderConstants.UploadChecksum).FirstOrDefault();
        }

        internal static bool HasDeclaredTrailingUploadChecksumHeader(this ContextAdapter context)
        {
            return context.HttpContext.Request.GetDeclaredTrailers().Any(x => x == HeaderConstants.UploadChecksum);
        }

#endif

        private static ITusFileLockProvider GetLockProvider(ContextAdapter context)
        {
            return context.Configuration.FileLockProvider ?? InMemoryFileLockProvider.Instance;
        }
    }
}
