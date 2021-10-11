using tusdotnet.Constants;

namespace tusdotnet.Parsers.UploadConcatParserHelpers
{
    internal class UploadConcatParserErrorTexts
    {
        internal static string HEADER_IS_INVALID = $"Header {HeaderConstants.UploadConcat}: Header is invalid. Valid values are \"partial\" and \"final\" followed by a list of file urls to concatenate";
    }
}
