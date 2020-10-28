using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Parsers
{
    internal static class UploadChallengeParser
    {
        public static UploadChallengeParserResult ParseAndValidate(string uploadChallengeHeaderValue, IEnumerable<string> supportedAlgorithms)
        {
            var temp = uploadChallengeHeaderValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (temp.Length != 2)
            {
                return UploadChallengeParserResult.FromError("Upload-Challenge header has an invalid format");
            }

            if (!supportedAlgorithms.Contains(temp[0]))
            {
                return UploadChallengeParserResult.FromError("Upload-Challenge algorithm is not supported");
            }

            byte[] hash;
            try
            {
                hash = Convert.FromBase64String(temp[1]);
            }
            catch
            {
                return UploadChallengeParserResult.FromError("Upload-Challenge is not properly encoded as base64");
            }

            return UploadChallengeParserResult.FromResult(temp[0], hash);
        }
    }
}
