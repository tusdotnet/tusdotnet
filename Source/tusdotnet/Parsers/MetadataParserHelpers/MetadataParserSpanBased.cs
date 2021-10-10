#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using tusdotnet.Extensions.Internal;
using tusdotnet.Models;

namespace tusdotnet.Parsers.MetadataParserHelpers
{
    internal class MetadataParserSpanBased
    {
        internal static MetadataParserResult ParseAndValidate(string uploadMetadataHeaderValue)
        {
            if (string.IsNullOrEmpty(uploadMetadataHeaderValue))
                return MetadataParserResult.FromResult(new Dictionary<string, Metadata>());

            var span = uploadMetadataHeaderValue.AsSpan();
            var result = new Dictionary<string, Metadata>();

            int indexOfNextPair = -1;
            while (!span.IsEmpty)
            {
                indexOfNextPair = span.IndexOf(',');
                var pair = indexOfNextPair == -1 ? span : span[0..indexOfNextPair].TrimEnd();

                var indexOfSpaceInPair = pair.IndexOf(' ');

                ReadOnlySpan<char> key;
                ReadOnlySpan<char> value;

                if (indexOfSpaceInPair == -1)
                {
                    key = pair;
                    value = ReadOnlySpan<char>.Empty;
                }
                else
                {
                    key = pair[0..indexOfSpaceInPair].TrimEnd();
                    value = pair[(indexOfSpaceInPair + 1)..].TrimEnd();
                }

                if (value.IndexOf(' ') > -1)
                {
                    return MetadataParserResult.FromError(MetadataParserErrorTexts.INVALID_FORMAT_ALLOW_EMPTY_VALUES);
                }

                if (key.IsEmpty)
                {
                    return MetadataParserResult.FromError(MetadataParserErrorTexts.KEY_EMPTY);
                }

                var keyString = key.ToString();

                if (result.ContainsKey(keyString))
                {
                    return MetadataParserResult.FromError(MetadataParserErrorTexts.DUPLICATE_KEY_FOUND);
                }

                byte[] decodedValue = null;
                if (!value.IsEmpty)
                {
                    var validBase64 = false;
                    (validBase64, decodedValue) = value.TryDecodeBase64();
                    if (!validBase64)
                    {
                        return MetadataParserResult.FromError(MetadataParserErrorTexts.InvalidBase64Value(keyString));
                    }
                }

                result.Add(keyString, Metadata.FromBytes(decodedValue));

                span = indexOfNextPair == -1 ? ReadOnlySpan<char>.Empty : span[(indexOfNextPair + 1)..];
            }

            return MetadataParserResult.FromResult(result);
        }
    }
}

#endif