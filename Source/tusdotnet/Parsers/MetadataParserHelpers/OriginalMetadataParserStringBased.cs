using System;
using System.Collections.Generic;
using tusdotnet.Constants;
using tusdotnet.Models;

namespace tusdotnet.Parsers.MetadataParserHelpers
{
    internal class OriginalMetadataParserStringBased : IInternalMetadataParser
    {
        internal static OriginalMetadataParserStringBased Instance { get; } = new OriginalMetadataParserStringBased();

        private const string EMPTY_HEADER_RESULT = $"Header {HeaderConstants.UploadMetadata} must consist of one or more comma-separated key-value pairs";

        private OriginalMetadataParserStringBased()
        {
        }

        public MetadataParserResult GetResultForEmptyHeader()
        {
            return MetadataParserResult.FromError(EMPTY_HEADER_RESULT);
        }

        public MetadataParserResult ParseSingleItem(string metadataItem, ICollection<string> existingKeys)
        {
            var pairParts = metadataItem.Split(new[] { ' ' });

            if (pairParts.Length != 2)
            {
                return MetadataParserResult.FromError(MetadataParserErrorTexts.INVALID_FORMAT_ORIGINAL);
            }

            var key = pairParts[0];
            if (string.IsNullOrEmpty(key))
            {
                return MetadataParserResult.FromError(MetadataParserErrorTexts.KEY_EMPTY);
            }

            if (existingKeys.Contains(key))
            {
                return MetadataParserResult.FromError(MetadataParserErrorTexts.DUPLICATE_KEY_FOUND);
            }

            try
            {
                return MetadataParserResult.FromResult(key, Metadata.FromBytes(Convert.FromBase64String(pairParts[1])));
            }
            catch (FormatException)
            {
                return MetadataParserResult.FromError(MetadataParserErrorTexts.InvalidBase64Value(key));
            }
        }
    }
}
