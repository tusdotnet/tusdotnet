using System.Collections.Generic;

namespace tusdotnet.Parsers.MetadataParserHelpers
{
    internal interface IInternalMetadataParser
    {
        MetadataParserResult GetResultForEmptyHeader();

        MetadataParserResult ParseSingleItem(string metadataItem, ICollection<string> existingKeys);
    }
}