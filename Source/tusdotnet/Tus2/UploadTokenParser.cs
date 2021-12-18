#nullable enable
using System;
using System.Linq;

namespace tusdotnet.Tus2
{
    internal interface IUploadTokenParser
    {
        string? Parse(string? uploadTokenHeader);
    }

    internal class UploadTokenParser : IUploadTokenParser
    {
        public string? Parse(string? uploadTokenHeader)
        {
            if (string.IsNullOrWhiteSpace(uploadTokenHeader))
                return null;

            return IsSfBinary(uploadTokenHeader)
                ? ParseSfBinary(uploadTokenHeader)
                : uploadTokenHeader;
        }

        private static bool IsSfBinary(string uploadToken)
        {
            return uploadToken[0] == ':' && uploadToken.Last() == ':';
        }

        private static string? ParseSfBinary(string uploadToken)
        {
            var sfBinary = uploadToken.AsSpan()[1..^1].ToString();

            for (int i = 0; i < sfBinary.Length; i++)
            {
                if (!IsBase64Char(sfBinary[i]))
                    return null;
            }

            return sfBinary.ToString();
        }

        private static bool IsBase64Char(char c)
        {
            return !Char.IsWhiteSpace(c) || Char.IsDigit(c) || c == '+' || c == '/' || c == '=';
        }
    }
}
