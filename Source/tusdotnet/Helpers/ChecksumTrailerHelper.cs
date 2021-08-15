using tusdotnet.Models;
#if !trailingheaders
using System;
using System.Runtime.CompilerServices;
#endif

namespace tusdotnet.Helpers
{
    /// <summary>
    /// Helper for determining if a checksum provided to a ITusChecksumStore is a fallback for a faulty checksum-trailer.
    /// </summary>
    public static class ChecksumTrailerHelper
    {
        // Use 20 byte array to match the length of a real SHA-1 checksum.
        internal static readonly Checksum TrailingChecksumToUseIfRealTrailerIsFaulty = new("sha1", new byte[20]);

#if trailingheaders
        /// <summary>
        /// Identifies if the provided algorithm and checksum is the fallback provided by tusdotnet to an instance of
        /// <c>ITusChecksumStore</c> when the client disconnected before the checksum-trailer could be read OR if the checksum-trailer is invalid (e.g. wrong format).
        /// This method will always return false on platforms that does not support trailing request headers.
        /// </summary>
        /// <param name="algorithm">The algorithm passed to <c>VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)</c></param>
        /// <param name="checksum">The checksum passed to <c>VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)</c></param>
        /// <returns>True if the provided checksum is a fallback, otherwise false</returns>
        public static bool IsFallback(string algorithm, byte[] checksum)
        {
            return algorithm == TrailingChecksumToUseIfRealTrailerIsFaulty.Algorithm
                && checksum == TrailingChecksumToUseIfRealTrailerIsFaulty.Hash;
        }
#else
        /// <summary>
        /// Identifies if the provided algorithm and checksum is the fallback provided by tusdotnet to an instance of
        /// <c>ITusChecksumStore</c> when the client disconnected before the checksum-trailer could be read OR if the checksum-trailer is invalid (e.g. wrong formatting etc).
        /// This method will always return false on platforms that does not support trailing request headers.
        /// </summary>
        /// <param name="algorithm">The algorithm passed to <c>VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)</c></param>
        /// <param name="checksum">The checksum passed to <c>VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)</c></param>
        /// <returns>True if the provided checksum is a fallback, otherwise false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFallback(string algorithm, byte[] checksum) => false;
#endif
    }
}
