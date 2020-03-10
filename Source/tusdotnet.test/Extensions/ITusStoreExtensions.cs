using NSubstitute;
using NSubstitute.Core;
using System;
using System.Threading;
using tusdotnet.Interfaces;

namespace tusdotnet.test.Extensions
{
    internal static class ITusStoreExtensions
    {
        internal static ITusStore WithExistingFile(this ITusStore store, string fileId, long? uploadLength = 1, long uploadOffset = 0)
        {
            return store.WithExistingFile(fileId, _ => uploadLength, _ => uploadOffset);
        }

        internal static ITusStore WithExistingFile(this ITusStore store, string fileId, Func<CallInfo, long?> uploadLength, Func<CallInfo, long> uploadOffset)
        {
            store.FileExistAsync(fileId, Arg.Any<CancellationToken>()).Returns(true);
            store.GetUploadLengthAsync(fileId, Arg.Any<CancellationToken>()).Returns(uploadLength);
            store.GetUploadOffsetAsync(fileId, Arg.Any<CancellationToken>()).Returns(uploadOffset);
            return store;
        }
    }
}
