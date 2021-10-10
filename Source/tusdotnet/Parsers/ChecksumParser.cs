using tusdotnet.Parsers.ChecksumParserHelpers;

namespace tusdotnet.Parsers
{
    internal static class ChecksumParser
    {
        internal static ChecksumParserResult ParseAndValidate(string uploadChecksumHeader)
        {
#if NETCOREAPP3_1_OR_GREATER
            return ChecksumParserSpanBased.ParseAndValidate(uploadChecksumHeader);
#else
            return ChecksumParserStringBased.ParseAndValidate(uploadChecksumHeader);
#endif
        }
    }
}
