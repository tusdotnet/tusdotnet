using System;
using System.Collections.Generic;

namespace tusdotnet.Models.Concatenation
{
	internal class UploadConcat
	{
		public FileConcat Type { get; }
		public bool IsValid { get; private set; }
		public string ErrorMessage { get; private set; }

		public UploadConcat(string uploadConcat) : this(uploadConcat, string.Empty)
		{
			// Left blank.
		}

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

		// ReSharper disable once SuggestBaseTypeForParameter
		private FileConcatFinal ParseFinal(string[] temp, string urlPath)
		{
			if (temp.Length < 2)
			{
				IsValid = false;
				ErrorMessage = "Unable to parse Upload-Concat header";
				return null;
			}

			var fileIds = new List<string>();

			foreach (var fileUri in temp[1].Split(' '))
			{
				Uri uri;
				if (!Uri.TryCreate(fileUri, UriKind.RelativeOrAbsolute, out uri))
				{
					IsValid = false;
					ErrorMessage = "Unable to parse Upload-Concat header";
					break;
				}

				var localPath = uri.IsAbsoluteUri
					? uri.LocalPath
					: uri.ToString();

				if (!localPath.StartsWith(urlPath))
				{
					IsValid = false;
					ErrorMessage = "Unable to parse Upload-Concat header";
					break;
				}

				fileIds.Add(localPath.Substring(urlPath.Length).Trim('/'));
			}

			return new FileConcatFinal(fileIds.ToArray());
		}

		//public static string CreateHeader(ConcatenationType type, string[] files = null)
		//{
		//	switch (type)
		//	{
		//		case ConcatenationType.None:
		//			return null;
		//		case ConcatenationType.Final:
		//			return files == null ? null : $"final {string.Join(";", files)}";
		//		case ConcatenationType.Partial:
		//			return "partial";
		//		default:
		//			return null;
		//	}
		//}
	}
}
