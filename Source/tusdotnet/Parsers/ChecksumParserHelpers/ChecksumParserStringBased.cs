#if !NETCOREAPP3_1_OR_GREATER

using System;

namespace tusdotnet.Parsers.ChecksumParserHelpers
{
    internal class ChecksumParserStringBased
    {
        internal static ChecksumParserResult ParseAndValidate(string uploadChecksumHeader)
        {
			var temp = uploadChecksumHeader.Split(' ');

			if (temp.Length != 2)
			{
				return ChecksumParserResult.FromError();
			}

			if (string.IsNullOrWhiteSpace(temp[0]))
			{
				return ChecksumParserResult.FromError();
			}

			var algorithm = temp[0].Trim();

			if (string.IsNullOrWhiteSpace(temp[1]))
			{
				return ChecksumParserResult.FromError();
			}

			try
			{

				var hash = Convert.FromBase64String(temp[1]);
				return ChecksumParserResult.FromResult(algorithm, hash);
			}
			catch
			{
				return ChecksumParserResult.FromError();
			}
		}
    }
}

#endif