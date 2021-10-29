using tusdotnet.Models;
using tusdotnet.Parsers.MetadataParserHelpers;

namespace tusdotnet.Parsers
{
    /// <summary>
    /// Parser that converts the provided Upload-Metadata header into a data structure more suitable for code. 
    /// </summary>
    public static class MetadataParser
    {
        /// <summary>
        /// Parse and validate the provided <paramref name="uploadMetadataHeaderValue"/> into a class structure.
        /// </summary>
        /// <param name="strategy">The strategy to use when parsing. <c>MetadataParsingStrategy.Original</c> requires that the metadata provided is composed of key value pairs while <c>MetadataParsingStrategy.AllowEmptyValues</c> allows the use of metadata keys without values.</param>
        /// <param name="uploadMetadataHeaderValue"></param>
        /// <returns>A <see cref="MetadataParserResult"/> containing the result of the operation.</returns>
        public static MetadataParserResult ParseAndValidate(MetadataParsingStrategy strategy, string uploadMetadataHeaderValue)
        {
            /* 
            * The Upload-Metadata request and response header MUST consist of one or more comma-separated key-value pairs.
            * The key and value MUST be separated by a space. 
            * The key MUST NOT contain spaces and commas and MUST NOT be empty. 
            * The key SHOULD be ASCII encoded and the value MUST be Base64 encoded. 
            * All keys MUST be unique. 
            * The value MAY be empty. 
            * In these cases, the space, which would normally separate the key and the value, MAY be left out.
            * 
            * NOTE: 
            *   Empty values were not allowed from the beginning and was added later. 
            *   This is why we use a MetadataParserStrategy.
            *   MetadataParserStrategy.AllowEmptyValues will also allow an present but empty Upload-Metadata header
            *   to help with compatibility with clients that are less than ideal.
            * */

#if NETCOREAPP3_1_OR_GREATER

            return strategy == MetadataParsingStrategy.AllowEmptyValues
                ? MetadataParserSpanBased.ParseAndValidate(uploadMetadataHeaderValue)
                : MetadataParserStringBased.ParseAndValidate(OriginalMetadataParserStringBased.Instance, uploadMetadataHeaderValue);

#else 

            var parser = (IInternalMetadataParser)(strategy == MetadataParsingStrategy.Original ? OriginalMetadataParserStringBased.Instance : AllowEmptyValuesMetadataParserStringBased.Instance);
            return MetadataParserStringBased.ParseAndValidate(parser, uploadMetadataHeaderValue);

#endif
        }
    }
}
