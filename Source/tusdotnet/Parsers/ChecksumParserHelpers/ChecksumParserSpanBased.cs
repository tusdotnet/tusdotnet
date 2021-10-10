#if NETCOREAPP3_1_OR_GREATER

using System;
using tusdotnet.Extensions.Internal;

namespace tusdotnet.Parsers.ChecksumParserHelpers
{
    internal static class ChecksumParserSpanBased
    {
        internal static ChecksumParserResult ParseAndValidate(string uploadChecksumHeader)
        {
            var span = uploadChecksumHeader.AsSpan();

            var indexOfSpace = span.IndexOf(' ');

            if (indexOfSpace == -1)
            {
                return ChecksumParserResult.FromError();
            }

            var algorithm = span[0..indexOfSpace].Trim();
            var hash = span[(indexOfSpace + 1)..].Trim();

            if (algorithm.IsEmpty || hash.IsEmpty)
            {
                return ChecksumParserResult.FromError();
            }

            var (validBase64, decodedValue) = hash.TryDecodeBase64();
            if (!validBase64)
            {
                return ChecksumParserResult.FromError();
            }

            return ChecksumParserResult.FromResult(algorithm.ToString(), decodedValue);
        }
    }
}

#endif