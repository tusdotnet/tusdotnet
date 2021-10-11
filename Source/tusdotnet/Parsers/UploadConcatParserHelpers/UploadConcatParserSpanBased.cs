#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Runtime.CompilerServices;
using tusdotnet.Models.Concatenation;

namespace tusdotnet.Parsers.UploadConcatParserHelpers
{
    internal static class UploadConcatParserSpanBased
    {
        private static readonly ReadOnlyMemory<char> _httpProtocol = "http://".AsMemory();
        private static readonly ReadOnlyMemory<char> _httpsProtocol = "https://".AsMemory();
        private static readonly FileConcatPartial _partial = new();

        internal static UploadConcatParserResult ParseAndValidate(string uploadConcatHeader, string urlPath)
        {
            var span = uploadConcatHeader.AsSpan();

            if (IsPartial(span))
            {
                return UploadConcatParserResult.FromResult(_partial);
            }

            if (IsFinal(span))
            {
                return ParseFinal(span, urlPath.AsSpan());
            }

            return UploadConcatParserResult.FromError(UploadConcatParserErrorTexts.HEADER_IS_INVALID);
        }

        private static UploadConcatParserResult ParseFinal(ReadOnlySpan<char> span, ReadOnlySpan<char> urlPath)
        {
            var indexOfFileListStart = span.IndexOf(';') + 1;

            span = span[indexOfFileListStart..];

            var fileIds = new string[CountFilesInFinalList(span)];
            var fileIdIndex = 0;

            while (!span.IsEmpty)
            {
                var indexOfSpace = span.IndexOf(' ');

                var fileUri = indexOfSpace == -1 ? span : span[0..indexOfSpace];

                var localPath = GetLocalPath(fileUri);

                if (localPath.IsEmpty || !localPath.StartsWith(urlPath))
                {
                    return UploadConcatParserResult.FromError(UploadConcatParserErrorTexts.HEADER_IS_INVALID);
                }

                var fileId = ExtractFileId(localPath, urlPath);

                fileIds[fileIdIndex++] = fileId.ToString();

                span = indexOfSpace == -1 ? ReadOnlySpan<char>.Empty : span[indexOfSpace..].Trim(' ');
            }

            return UploadConcatParserResult.FromResult(new FileConcatFinal(fileIds));

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountFilesInFinalList(this ReadOnlySpan<char> value)
        {
            var numberOfCharacters = 0;

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == ' ')
                {
                    numberOfCharacters++;
                }
            }

            return numberOfCharacters + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> ExtractFileId(ReadOnlySpan<char> localPath, ReadOnlySpan<char> urlPath)
        {
            var fileId = localPath[urlPath.Length..].Trim('/');
            var indexOfQuestionMarkOrHash = fileId.IndexOfAny('?', '#');

            return indexOfQuestionMarkOrHash != -1
                ? fileId[0..indexOfQuestionMarkOrHash]
                : fileId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> GetLocalPath(ReadOnlySpan<char> fileUri)
        {
            if (fileUri.IsEmpty)
                return ReadOnlySpan<char>.Empty;

            var result = TryParseLocalPathFromFullUri(fileUri);

            if (!result.IsEmpty)
                return result;

            return fileUri;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> TryParseLocalPathFromFullUri(ReadOnlySpan<char> fileUri)
        {
            var isHttp = fileUri.StartsWith(_httpProtocol.Span);
            bool isHttps = false;

            if (!isHttp)
            {
                isHttps = fileUri.StartsWith(_httpsProtocol.Span);
            }

            if (isHttp || isHttps)
            {
                var protocolLength = isHttp ? 7 : 8; // "http://" == 7
                var httpUri = fileUri[protocolLength..];
                var indexOfSlash = httpUri.IndexOf('/');
                if (indexOfSlash == -1)
                    return ReadOnlySpan<char>.Empty;

                httpUri = httpUri[indexOfSlash..];
                return httpUri;
            }

            return ReadOnlySpan<char>.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinal(ReadOnlySpan<char> span)
        {
            return span.Length > 6
                && span[0] == 'f'
                && span[1] == 'i'
                && span[2] == 'n'
                && span[3] == 'a'
                && span[4] == 'l'
                && span[5] == ';';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPartial(ReadOnlySpan<char> span)
        {
            return span.Length == 7
                && span[0] == 'p'
                && span[1] == 'a'
                && span[2] == 'r'
                && span[3] == 't'
                && span[4] == 'i'
                && span[5] == 'a'
                && span[6] == 'l';
        }
    }
}

#endif