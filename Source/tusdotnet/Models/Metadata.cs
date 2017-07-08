using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tusdotnet.Constants;

namespace tusdotnet.Models
{
	/// <summary>
	/// Container for uploaded file metadata.
	/// </summary>
	public class Metadata
	{
		private readonly string _encodedValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="Metadata"/> class.
		/// </summary>
		/// <param name="encodedValue">The encoded value of a single Upload-Metadata value</param>
		/// <exception cref="ArgumentNullException">If the encoded value is null or empty</exception>
		private Metadata(string encodedValue)
		{
			if (string.IsNullOrWhiteSpace(encodedValue))
			{
				throw new ArgumentNullException(nameof(encodedValue));
			}

			_encodedValue = encodedValue;
		}

		/// <summary>
		/// Returns the raw byte array of the decoded value.
		/// </summary>
		/// <returns>The raw byte array of the encoded value</returns>
		public byte[] GetBytes()
		{
			return Convert.FromBase64String(_encodedValue);
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

			return encoding.GetString(GetBytes());
		}

		/// <summary>
		/// Parse the provided Upload-Metadata header into a data structure
		/// more suitable for code.
		/// </summary>
		/// <param name="uploadMetadata">The Upload-Metadata header provided during the creation process</param>
		/// <returns></returns>
		public static Dictionary<string, Metadata> Parse(string uploadMetadata)
		{
			/* Cannot return Dictionary<string, string> here as the metadata might not be a string:
			 * "Yes, the value, which is going to be Base64 encoded, does not necessarily have to be an UTF8 (or similar) string. 
			 * Theoretically, it can also be raw binary data, as you asked. 
			 * In the end, it's still the server which decides whether it's going to use it or not."
			 * Source: http://tus.io/protocols/resumable-upload.html#comment-2893439572
			 * */

			var dictionary = new Dictionary<string, Metadata>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrWhiteSpace(uploadMetadata))
			{
				return dictionary;
			}

			var metadataPairs = uploadMetadata.Split(',');
			foreach (var pair in metadataPairs)
			{
				var keyAndValue = pair.Split(' ');
				var key = keyAndValue[0];
				var base64Value = keyAndValue[1];
				var value = new Metadata(base64Value);

				dictionary[key] = value;
			}

			return dictionary;
		}

		/// <summary>
		/// Validate the provided Upload-Metadata header.
		/// Returns an error message or null if the validation passes.
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

			if (string.IsNullOrEmpty(metadata))
			{
				return $"Header {HeaderConstants.UploadMetadata} must consist of one or more comma-separated key-value pairs";
			}

			var keys = new HashSet<string>();
			var pairs = metadata.Split(',');
			foreach (var pairParts in pairs.Select(pair => pair.Split(' ')))
			{
				if (pairParts.Length != 2)
				{
					return $"Header {HeaderConstants.UploadMetadata}: The Upload-Metadata request and response header MUST consist of one or more comma - separated key - value pairs. The key and value MUST be separated by a space.The key MUST NOT contain spaces and commas and MUST NOT be empty. The key SHOULD be ASCII encoded and the value MUST be Base64 encoded.All keys MUST be unique.";
				}

				var key = pairParts.First();
				if (string.IsNullOrEmpty(key))
				{
					return $"Header {HeaderConstants.UploadMetadata}: Key must not be empty";
				}

				if (keys.Contains(key))
				{
					return $"Header {HeaderConstants.UploadMetadata}: Duplicate keys are not allowed";
				}

				var value = pairParts.Skip(1).First();

				try
				{
					// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
					Convert.FromBase64String(value);
				}
				catch (FormatException)
				{
					return $"Header {HeaderConstants.UploadMetadata}: Value for {key} is not properly encoded using base64";
				}

				keys.Add(key);
			}

			return null;
		}
	}
}
