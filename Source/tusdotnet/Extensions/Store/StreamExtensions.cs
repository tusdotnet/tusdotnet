#nullable enable
using System.IO;
using System.Threading;
using tusdotnet.Helpers;
using tusdotnet.Models.Streams;

namespace tusdotnet.Extensions.Store
{
    /// <summary>
    /// Extension methods for <see cref="Stream"/> to integrate with features in tusdotnet.
    /// Methods in this class are designed to be ran from <see cref="Interfaces.ITusStore.AppendDataAsync(string, Stream, System.Threading.CancellationToken)"/>
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Returns information about the Upload-Checksum header provided by the client.
        /// If the client did not provide the header, or if the store does not implement <see cref="Interfaces.ITusChecksumStore"/>, this method will return null.
        /// </summary>
        /// <param name="stream">The Stream provided to AppendDataAsync</param>
        /// <returns>Information about the Upload-Checksum header or null</returns>
        public static ChecksumInfo? GetUploadChecksumInfo(this Stream stream)
        {
            return stream is ChecksumAwareStream checksumStream
                ? new() { Algorithm = checksumStream.Checksum.Algorithm }
                : null;
        }

        /// <summary>
        /// Returns a token that is cancelled when the current request's file lock is lost, e.g. when a distributed
        /// lock's lease expires during a long upload. Another request may then own the file, so the store must not
        /// persist this request's data. Throw a <see cref="Models.TusUploadConflictException"/>, or let an
        /// <see cref="System.OperationCanceledException"/> caused by this token propagate, to answer 409 Conflict.
        /// Returns <see cref="CancellationToken.None"/> unless the lock implements <see cref="Interfaces.ITusLeasedFileLock"/>.
        /// Call it on the Stream passed to AppendDataAsync, not on a wrapper around it: a wrapper also returns
        /// <see cref="CancellationToken.None"/>.
        /// </summary>
        /// <remarks>
        /// The lock can be lost right after the store checks the token. For correctness, use a conditional write
        /// against the storage (e.g. an ETag precondition) and throw <see cref="Models.TusUploadConflictException"/>
        /// when it fails.
        /// </remarks>
        /// <param name="stream">The Stream provided to AppendDataAsync</param>
        /// <returns>The token that is cancelled when the file lock is lost, or <see cref="CancellationToken.None"/></returns>
        public static CancellationToken GetFileLockLostToken(this Stream stream)
        {
            return FileLockLostTokenRegistry.Get(stream);
        }
    }
}
