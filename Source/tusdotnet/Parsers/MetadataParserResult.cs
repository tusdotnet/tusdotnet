using System.Collections.Generic;
using tusdotnet.Models;

namespace tusdotnet.Parsers
{
    /// <summary>
    /// Result of a call to <c>MetadataParser.ParseAndValidate</c>.
    /// </summary>
    public sealed class MetadataParserResult
    {
        /// <summary>
        /// True if the parsing was successful, otherwise false.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Error mesage if <see cref="Success"/> is false, otherwise null.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Metadata that was parsed. If <see cref="Success"/> is false then this will contain an empty dictionary.
        /// </summary>
        public Dictionary<string, Metadata> Metadata { get; }

        private MetadataParserResult(bool success, string errorMessage, Dictionary<string, Metadata> metadata)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Metadata = metadata;
        }

        internal static MetadataParserResult FromError(string errorMessage)
        {
            return new MetadataParserResult(success: false, errorMessage, new Dictionary<string, Metadata>());
        }

        internal static MetadataParserResult FromResult(Dictionary<string, Metadata> metadata)
        {
            return new MetadataParserResult(success: true, errorMessage: null, metadata);
        }

        internal static MetadataParserResult FromResult(string key, Metadata metadata)
        {
            return FromResult(new Dictionary<string, Metadata>
            {
                { key, metadata }
            });
        }
    }
}
