using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Parsers
{
    internal sealed class UploadChallengeParserResult
    {
        // TODO: Move to common base class

        /// <summary>
        /// True if the parsing was successful, otherwise false.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Error mesage if <see cref="Success"/> is false, otherwise null.
        /// </summary>
        public string ErrorMessage { get; }

        public string Algorithm { get; set; }

        public byte[] Hash { get; set; }

        private UploadChallengeParserResult(bool success, string errorMessage, string algorithm = null, byte[] hash = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Algorithm = algorithm;
            Hash = hash;
        }

        internal static UploadChallengeParserResult FromResult(string algorithm, byte[] hash)
        {
            return new UploadChallengeParserResult(success: true, errorMessage: null, algorithm, hash);
        }

        internal static UploadChallengeParserResult FromError(string errorMessage)
        {
            return new UploadChallengeParserResult(success: false, errorMessage);
        }
    }
}
