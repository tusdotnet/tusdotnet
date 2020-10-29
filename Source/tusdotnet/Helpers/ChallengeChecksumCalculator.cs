using System.Security.Cryptography;
using System.Text;
using tusdotnet.Interfaces;

namespace tusdotnet.Helpers
{
    internal sealed class ChallengeChecksumCalculator : ITusChallengeStoreHashFunction
    {
        internal static ITusChallengeStoreHashFunction Sha256 { get; } = new ChallengeChecksumCalculator();

        internal static string[] SupportedAlgorithms = new[] { "sha256" };

        private ChallengeChecksumCalculator()
        {
        }

        public byte[] ComputeHash(string input) => CalculateSha256(input);

#if netfull

        public static byte[] CalculateSha256(string valueToHash)
        {
            byte[] fileHash;
            using (var sha256 = new SHA256Managed())
            {
                fileHash = sha256.ComputeHash(new ASCIIEncoding().GetBytes(valueToHash));
            }

            return fileHash;
        }

#endif

#if netstandard

        public static byte[] CalculateSha256(string valueToHash)
        {
            byte[] fileHash;
            using (var sha256 = SHA256.Create())
            {
                // TODO: Use overload that takes a byte array and use a rented array
                fileHash = sha256.ComputeHash(new ASCIIEncoding().GetBytes(valueToHash));
            }

            return fileHash;
        }

#endif
    }
}
