using System;
using System.Collections.Generic;

namespace tusdotnet.Models.Concatenation
{
	/// <summary>
	/// Container for uploaded file concatenation information.
	/// </summary>
	public class UploadConcat
	{
		/// <summary>
		/// The type of concatenation used. Is null if no concatenation info was provided or if the info is invalid.
		/// </summary>
		public FileConcat Type { get; }

		/// <summary>
		/// True if the header value was parsable and the info therein was valid, otherwise false.
		/// </summary>
		public bool IsValid { get; private set; }

		/// <summary>
		/// Parser error message. Null if <code>IsValid</code> is true.
		/// </summary>
		public string ErrorMessage { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="UploadConcat"/> class.
		/// This overload does not remove relative urls from the file ids when parsing.
		/// This overload should only be used inside a data store to save information regarding the concatenation type.
		/// </summary>
		/// <param name="uploadConcat">The Upload-Concat header</param>
		public UploadConcat(string uploadConcat) : this(uploadConcat, string.Empty)
		{
			// Left blank.
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="UploadConcat"/> class.
		/// This overload removes relative urls from the file ids when parsing and is used by tusdotnet when parsing
		/// the incoming Upload-Concat header.
		/// </summary>
		/// <param name="uploadConcat">The Upload-Concat header</param>
		/// <param name="urlPath">The UrlPath property in the ITusConfiguration</param>
		public UploadConcat(string uploadConcat, string urlPath)
		{
			IsValid = true;

			if (string.IsNullOrWhiteSpace(uploadConcat))
			{
				Type = null;
				return;
			}

			var temp = uploadConcat.Split(';');

			// Unable to parse Upload-Concat header
			var type = temp[0].ToLower();
			switch (type)
			{
				case "partial":
					Type = new FileConcatPartial();
					break;
				case "final":
					Type = ParseFinal(temp, urlPath);
					break;
				default:
					IsValid = false;
					ErrorMessage = "Upload-Concat header is invalid. Valid values are \"partial\" and \"final\" followed by a list of files to concatenate";
					return;
			}
		}

		/// <summary>
		/// Parses the "final" concatenation type based on the parts provided.
		/// Will validate and strip the url path provided to make sure that all files are in the same store.
		/// </summary>
		/// <param name="parts">The separated parts of the Upload-Concat header</param>
		/// <param name="urlPath">The UrlPath property in the ITusConfiguration</param>
		/// <returns>THe parse final concatenation</returns>
		// ReSharper disable once SuggestBaseTypeForParameter
		private FileConcatFinal ParseFinal(string[] parts, string urlPath)
		{
			if (parts.Length < 2)
			{
				IsValid = false;
				ErrorMessage = "Unable to parse Upload-Concat header";
				return null;
			}

		    var fileUris = parts[1].Split(' ');
		    var fileIds = new List<string>(fileUris.Length);

            foreach (var fileUri in fileUris)
			{
				if (string.IsNullOrWhiteSpace(fileUri) || !Uri.TryCreate(fileUri, UriKind.RelativeOrAbsolute, out Uri uri))
				{
					IsValid = false;
					ErrorMessage = "Unable to parse Upload-Concat header";
					break;
				}

				var localPath = uri.IsAbsoluteUri
					? uri.LocalPath
					: uri.ToString();

				if (!localPath.StartsWith(urlPath, StringComparison.OrdinalIgnoreCase))
				{
					IsValid = false;
					ErrorMessage = "Unable to parse Upload-Concat header";
					break;
				}

				fileIds.Add(localPath.Substring(urlPath.Length).Trim('/'));
			}

			return new FileConcatFinal(fileIds.ToArray());
		}
	}
}
