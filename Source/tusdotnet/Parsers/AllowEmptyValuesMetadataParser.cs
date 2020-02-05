using System;
using System.Collections.Generic;
using tusdotnet.Constants;
using tusdotnet.Models;

namespace tusdotnet.Parsers
{
    internal class AllowEmptyValuesMetadataParser : IInternalMetadataParser
    {
        public MetadataParserResult GetResultForEmptyHeader()
        {
            return MetadataParserResult.FromResult(new Dictionary<string, Metadata>());
        }

        public MetadataParserResult ParseSingleItem(string metadataItem, ICollection<string> existingKeys)
        {
            var pairParts = metadataItem.TrimEnd(' ').Split(new[] { ' ' });

            if (pairParts.Length < 1 || pairParts.Length > 2)
            {
                return MetadataParserResult.FromError($"Header {HeaderConstants.UploadMetadata}: The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs. The key and value MUST be separated by a space.The key MUST NOT contain spaces and commas and MUST NOT be empty. The key SHOULD be ASCII encoded and the value MUST be Base64 encoded. All keys MUST be unique. The value MAY be empty. In these cases, the space, which would normally separate the key and the value, MAY be left out.");
            }

            var key = pairParts[0];
            if (string.IsNullOrEmpty(key))
            {
                return MetadataParserResult.FromError($"Header {HeaderConstants.UploadMetadata}: Key must not be empty");
            }

            if (existingKeys.Contains(key))
            {
                return MetadataParserResult.FromError($"Header {HeaderConstants.UploadMetadata}: Duplicate keys are not allowed");
            }

            try
            {
                return MetadataParserResult.FromResult(key, GetMetadataValue(pairParts));
            }
            catch (FormatException)
            {
                return MetadataParserResult.FromError($"Header {HeaderConstants.UploadMetadata}: Value for {key} is not properly encoded using base64");
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
