using System.Collections.Generic;
using System.Linq;
using tusdotnet.Models;

namespace tusdotnet.Parsers.MetadataParserHelpers
{
    internal class MetadataParserStringBased
    {
        internal static MetadataParserResult ParseAndValidate(IInternalMetadataParser parser, string uploadMetadataHeaderValue)
        {
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
    }
}
