using System.Runtime.CompilerServices;
using tusdotnet.Constants;

namespace tusdotnet.Parsers.MetadataParserHelpers
{
    internal class MetadataParserErrorTexts
    {
        internal const string INVALID_FORMAT_ALLOW_EMPTY_VALUES = $"Header {HeaderConstants.UploadMetadata}: The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs. The key and value MUST be separated by a space.The key MUST NOT contain spaces and commas and MUST NOT be empty. The key SHOULD be ASCII encoded and the value MUST be Base64 encoded. All keys MUST be unique. The value MAY be empty. In these cases, the space, which would normally separate the key and the value, MAY be left out.";
        internal const string INVALID_FORMAT_ORIGINAL = $"Header {HeaderConstants.UploadMetadata}: The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs. The key and value MUST be separated by a space.The key MUST NOT contain spaces and commas and MUST NOT be empty. The key SHOULD be ASCII encoded and the value MUST be Base64 encoded. All keys MUST be unique.";
        internal const string KEY_EMPTY = $"Header {HeaderConstants.UploadMetadata}: Key must not be empty";
        internal const string DUPLICATE_KEY_FOUND = $"Header {HeaderConstants.UploadMetadata}: Duplicate keys are not allowed";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string InvalidBase64Value(string metadataKey) => $"Header {HeaderConstants.UploadMetadata}: Value for {metadataKey} is not properly encoded using base64";
    }
}
