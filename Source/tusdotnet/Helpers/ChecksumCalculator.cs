using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Helpers
{
    internal static class ChecksumCalculator
    {
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
