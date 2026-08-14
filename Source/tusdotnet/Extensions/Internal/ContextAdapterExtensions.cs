#nullable enable

using System;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.FileLocks;
using tusdotnet.Interfaces;
using tusdotnet.Models;
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
            return lockProvider.AquireLock(context.FileId);
        }

#if trailingheaders

        [Obsolete("Use context method directly")]
        internal static string? GetTrailingUploadChecksumHeader(this ContextAdapter context)
        {
            return context.TrailingHeaderHelper.GetTrailingUploadChecksumHeader();
        }

        [Obsolete("Use context method directly")]
        internal static bool HasDeclaredTrailingUploadChecksumHeader(this ContextAdapter context)
        {
            return context.TrailingHeaderHelper.HasDeclaredTrailingUploadChecksumHeader();
        }
#endif

        private static ITusFileLockProvider GetLockProvider(ContextAdapter context)
        {
            return context.Configuration.FileLockProvider ?? InMemoryFileLockProvider.Instance;
        }

        internal static async Task<MaxReadSizeInfo> GetMaxReadSizeInfo(this ContextAdapter context)
        {
            // We can use the upload offset in the request here as this has been verified earlier in the pipeline.
            var previouslyRead = context.Request.Headers.UploadOffset;
            var sizeSource = MaxReadSizeExceededException.SizeSourceType.UploadLength;

            var maxLength = await context.StoreAdapter.GetUploadLengthAsync(
                context.FileId,
                context.CancellationToken
            );
            if (maxLength == null)
            {
                maxLength = context.Configuration.GetMaxAllowedUploadSizeInBytes();
                sizeSource = MaxReadSizeExceededException.SizeSourceType.TusMaxSize;
            }

            return new MaxReadSizeInfo
            {
                MaxReadSize = maxLength,
                PreviouslyRead = previouslyRead,
                SizeSource = sizeSource,
            };
        }

        internal struct MaxReadSizeInfo
        {
            public long PreviouslyRead { get; set; }

            public long? MaxReadSize { get; set; }

            public MaxReadSizeExceededException.SizeSourceType SizeSource { get; set; }
        }
    }
}
