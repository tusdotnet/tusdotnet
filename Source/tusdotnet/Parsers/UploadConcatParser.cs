using tusdotnet.Parsers.UploadConcatParserHelpers;

namespace tusdotnet.Parsers
{
    internal static class UploadConcatParser
    {
        internal static UploadConcatParserResult ParseAndValidate(string uploadConcatHeader, string urlPath)
        {
#if NETCOREAPP3_1_OR_GREATER
    
            return UploadConcatParserSpanBased.ParseAndValidate(uploadConcatHeader, urlPath);
#else
            return UploadConcatParserStringBased.ParseAndValidate(uploadConcatHeader, urlPath);

#endif
        }
	}
}
