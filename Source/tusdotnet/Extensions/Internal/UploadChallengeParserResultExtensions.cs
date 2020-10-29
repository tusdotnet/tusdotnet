using System.Linq;
using tusdotnet.Interfaces;
using tusdotnet.Parsers;

namespace tusdotnet.Extensions.Internal
{
    internal static class UploadChallengeParserResultExtensions
    {
        internal static bool AssertUploadChallengeIsProvidedIfSecretIsSet(this UploadChallengeParserResult uploadChallenge, string secret)
        {
            if (uploadChallenge == null && !string.IsNullOrEmpty(secret))
                return false;

            return true;
        }

        internal static bool VerifyChecksum(this UploadChallengeParserResult uploadChallenge, string uploadOffsetHeader, string httpMethod, string secret, ITusChallengeStoreHashFunction hashFunction)
        {
            uploadOffsetHeader ??= "#";
            httpMethod = httpMethod.ToUpper();

            return uploadChallenge.VerifyChecksum(httpMethod + uploadOffsetHeader + secret, hashFunction);
        }

        internal static bool VerifyChecksum(this UploadChallengeParserResult uploadChallenge, string dataToHash, ITusChallengeStoreHashFunction hashFunction)
        {
            var calculatedHash = hashFunction.ComputeHash(dataToHash);

            return calculatedHash.SequenceEqual(uploadChallenge.Hash);
        }
    }
}
