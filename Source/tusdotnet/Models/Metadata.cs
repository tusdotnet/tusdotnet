using System;
using System.Collections.Generic;
using System.Text;

namespace tusdotnet.Models
{
	public class Metadata
	{
		private readonly string _encodedValue;

		private Metadata(string encodedValue)
		{
			if (string.IsNullOrEmpty(encodedValue))
			{
				throw new ArgumentNullException(nameof(encodedValue));
			}

			_encodedValue = encodedValue;
		}

		public byte[] GetBytes()
		{
			return Convert.FromBase64String(_encodedValue);
		}

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

			var dictionary = new Dictionary<string, Metadata>();
			if (string.IsNullOrEmpty(uploadMetadata))
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
	}
}
