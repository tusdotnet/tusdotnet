#if !NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Parsers.UploadConcatParserHelpers
{
    internal class UploadConcatParserStringBased
    {
        internal static UploadConcatParserResult ParseAndValidate(string uploadConcatHeader, string urlPath)
        {
            var temp = uploadConcatHeader.Split(';');

            // Unable to parse Upload-Concat header
            var type = temp[0].ToLower();
            return type switch
            {
                "partial" => UploadConcatParserResult.FromResult(new FileConcatPartial()),
                "final" => ParseFinal(temp, urlPath),
                _ => UploadConcatParserResult.FromError(UploadConcatParserErrorTexts.HEADER_IS_INVALID),
            };
        }

        /// <summary>
        /// Parses the "final" concatenation type based on the parts provided.
        /// Will validate and strip the url path provided to make sure that all files are in the same store.
        /// </summary>
        /// <param name="parts">The separated parts of the Upload-Concat header</param>
        /// <param name="urlPath">The UrlPath property in the ITusConfiguration</param>
        /// <returns>THe parse final concatenation</returns>
        // ReSharper disable once SuggestBaseTypeForParameter
        private static UploadConcatParserResult ParseFinal(string[] parts, string urlPath)
        {
            if (parts.Length < 2)
            {
                return UploadConcatParserResult.FromError(UploadConcatParserErrorTexts.HEADER_IS_INVALID);
            }

            var fileUris = parts[1].Split(' ');
            var fileIds = new List<string>(fileUris.Length);

            foreach (var fileUri in fileUris)
            {
                if (string.IsNullOrWhiteSpace(fileUri) || !Uri.TryCreate(fileUri, UriKind.RelativeOrAbsolute, out Uri uri))
                {
                    return UploadConcatParserResult.FromError(UploadConcatParserErrorTexts.HEADER_IS_INVALID);
                }

                var localPath = uri.IsAbsoluteUri
                    ? uri.LocalPath
                    : uri.ToString();

                if (!localPath.StartsWith(urlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return UploadConcatParserResult.FromError(UploadConcatParserErrorTexts.HEADER_IS_INVALID);
                }

                fileIds.Add(localPath.Substring(urlPath.Length).Trim('/'));
            }

            return UploadConcatParserResult.FromResult(new FileConcatFinal(fileIds.ToArray()));
        }
    }
}

#endif