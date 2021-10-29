using System;
using System.Collections.Generic;
using System.Text;
using tusdotnet.Parsers;

namespace tusdotnet.Models
{
    /// <summary>
    /// Container for uploaded file metadata.
    /// </summary>
    public class Metadata
    {
        private static readonly byte[] _emptyBytes = new byte[0];

        private readonly byte[] _decodedValue;

        /// <summary>
        /// Returns true if there is no value associated with this metadata.
        /// </summary>
        public bool HasEmptyValue => _decodedValue == null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Metadata"/> class.
        /// </summary>
        /// <param name="decodedValue">The decoded value of a single Upload-Metadata value</param>
        private Metadata(byte[] decodedValue)
        {
            _decodedValue = decodedValue;
        }

        /// <summary>
        /// Returns the raw byte array of the decoded value.
        /// </summary>
        /// <returns>The raw byte array of the encoded value</returns>
        public byte[] GetBytes()
        {
            return _decodedValue ?? _emptyBytes;
        }

        /// <summary>
        /// Returns the text representation of the decoded value.
        /// </summary>
        /// <param name="encoding">The encoding to use when creating the text representation</param>
        /// <returns>The text representation of the decoded value</returns>
        public string GetString(Encoding encoding)
        {
            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            var bytes = GetBytes();

            if (bytes == null)
            {
                return string.Empty;
            }

            return encoding.GetString(bytes);
        }

        /// <summary>
        /// Parse the provided Upload-Metadata header into a data structure
        /// more suitable for code. 
        /// <para>
        /// NOTE: This methods uses the original parsing strategy. To allow empty values use <see cref="MetadataParser.ParseAndValidate(MetadataParsingStrategy, string)"/>
        /// </para>
        /// </summary>
        /// <param name="uploadMetadata">The Upload-Metadata header provided during the creation process</param>
        /// <returns></returns>
        public static Dictionary<string, Metadata> Parse(string uploadMetadata)
        {
            /* Cannot return Dictionary<string, string> here as the metadata might not be a string:
			 * "Yes, the value, which is going to be Base64 encoded, does not necessarily have to be an UTF8 (or similar) string. 
			 * Theoretically, it can also be raw binary data, as you asked. 
			 * In the end, it's still the server which decides whether it's going to use it or not."
			 * Source: https://tus.io/protocols/resumable-upload.html#comment-2893439572
			 * */

            var result = MetadataParser.ParseAndValidate(MetadataParsingStrategy.Original, uploadMetadata);
            return result.Metadata;
        }

        /// <summary>
        /// Validate the provided Upload-Metadata header.
        /// Returns an error message or null if the validation passes.
        /// <para>
        /// NOTE: This methods uses the original parsing strategy. To allow empty values use <see cref="MetadataParser.ParseAndValidate(MetadataParsingStrategy, string)"/>
        /// </para>
        /// </summary>
        /// <param name="metadata">The Upload-Metadata header</param>
        /// <returns>An error message or null if the validation passes</returns>
        public static string ValidateMetadataHeader(string metadata)
        {
            /* 
             * The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs.
             * The key and value MUST be separated by a space. The key MUST NOT contain spaces and commas and MUST NOT be empty.
             * The key SHOULD be ASCII encoded and the value MUST be Base64 encoded. All keys MUST be unique.
             * */

            var result = MetadataParser.ParseAndValidate(MetadataParsingStrategy.Original, metadata);
            return result.ErrorMessage;
        }

#if !NETCOREAPP3_1_OR_GREATER

        internal static Metadata FromEmptyValue()
        {
            return new Metadata(decodedValue: null);
        }

#endif

        internal static Metadata FromBytes(byte[] decodedValue)
        {
            return new Metadata(decodedValue);
        }
    }
}
