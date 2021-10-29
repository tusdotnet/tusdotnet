#if !NETCOREAPP3_1_OR_GREATER

// We are not using this parser but MetadataParserSpanBased.cs for netcoreapp3.1 and later when MetadataParsingStrategy is AllowEmptyValues.

using System;
using System.Collections.Generic;
using tusdotnet.Models;

namespace tusdotnet.Parsers.MetadataParserHelpers
{
    internal class AllowEmptyValuesMetadataParserStringBased : IInternalMetadataParser
    {
        internal static AllowEmptyValuesMetadataParserStringBased Instance { get; } = new AllowEmptyValuesMetadataParserStringBased();

        private AllowEmptyValuesMetadataParserStringBased()
        {
        }

        public MetadataParserResult GetResultForEmptyHeader()
        {
            return MetadataParserResult.FromResult(new Dictionary<string, Metadata>());
        }

        public MetadataParserResult ParseSingleItem(string metadataItem, ICollection<string> existingKeys)
        {
            var pairParts = metadataItem.TrimEnd(' ').Split(new[] { ' ' });

            if (pairParts.Length < 1 || pairParts.Length > 2)
            {
                return MetadataParserResult.FromError(MetadataParserErrorTexts.INVALID_FORMAT_ALLOW_EMPTY_VALUES);
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
                return MetadataParserResult.FromResult(key, GetMetadataValue(pairParts));
            }
            catch (FormatException)
            {
                return MetadataParserResult.FromError(MetadataParserErrorTexts.InvalidBase64Value(key));
            }
        }

        private Metadata GetMetadataValue(string[] pairParts)
        {
            string value = null;

            if (pairParts.Length == 2)
            {
                value = pairParts[1];
            }

            if (string.IsNullOrEmpty(value))
            {
                return Metadata.FromEmptyValue();
            }

            return Metadata.FromBytes(Convert.FromBase64String(value));
        }
    }
}

#endif