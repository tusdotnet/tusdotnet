using System.Collections.Generic;
using System.Linq;
using tusdotnet.Models;

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

            var parser = GetParser(strategy);

            if (string.IsNullOrWhiteSpace(uploadMetadataHeaderValue))
            {
                return parser.GetResultForEmptyHeader();
            }

            var splitMetadataHeader = uploadMetadataHeaderValue.Split(',');
            var parsedMetadata = new Dictionary<string, Metadata>(splitMetadataHeader.Length);

            foreach (var pair in splitMetadataHeader)
            {
                var singleItemParseResult = parser.ParseSingleItem(pair, parsedMetadata.Keys);

                if (singleItemParseResult.Success)
                {
                    var parsedKeyAndValue = singleItemParseResult.Metadata.First();
                    parsedMetadata.Add(parsedKeyAndValue.Key, parsedKeyAndValue.Value);
                }
                else
                {
                    return singleItemParseResult;
                }
            }

            return MetadataParserResult.FromResult(parsedMetadata);
        }

        private static IInternalMetadataParser GetParser(MetadataParsingStrategy strategy)
        {
            if (strategy == MetadataParsingStrategy.Original)
            {
                return new OriginalMetadataParser();
            }

            return new AllowEmptyValuesMetadataParser();
        }
    }
}
