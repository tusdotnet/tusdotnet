#nullable enable
using System;
using System.Text;

namespace tusdotnet.Tus2
{
    internal static class StructureFieldExtensions
    {
        public static bool? FromSfBool(this string? value)
        {
            return value switch
            {
                "?0" => false,
                "?1" => true,
                _ => null
            };
        }

        public static string ToSfBool(this bool value)
        {
            return value ? "?1" : "?0";
        }

        public static long? FromSfInteger(this string? value)
        {
            if (value == null)
                return null;

            var couldParse = long.TryParse(value, out long uploadOffset);

            return couldParse ? uploadOffset : null;
        }

        public static string? FromSfBinary(this string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!IsSfBinary(value))
                return null;

            return ParseSfBinary(value);

            static bool IsSfBinary(string value)
            {
                return value[0] == ':' && value[^1] == ':';
            }

            static string? ParseSfBinary(string value)
            {
                var sfBinary = value.AsSpan()[1..^1];

                for (int i = 0; i < sfBinary.Length; i++)
                {
                    if (!IsBase64Char(sfBinary[i]))
                        return null;
                }

                return sfBinary.ToString();
            }

            static bool IsBase64Char(char c)
            {
                return !char.IsWhiteSpace(c) || char.IsAscii(c) || char.IsDigit(c) || c == '+' || c == '/' || c == '=';
            }
        }

        public static string? ToSfDictionary(this TusHandlerLimits limits)
        {
            var sb = new StringBuilder();

            var expires = (long?)(limits.Expiration?.TotalSeconds);

            AppendIfNotNull(sb, "max-size", limits.MaxSize);
            AppendIfNotNull(sb, "min-size", limits.MinSize);
            AppendIfNotNull(sb, "max-append-size", limits.MaxAppendSize);
            AppendIfNotNull(sb, "min-append-size", limits.MinAppendSize);
            AppendIfNotNull(sb, "expires", expires);

            return sb.ToString().TrimEnd(',');

            static void AppendIfNotNull(StringBuilder sb, string key, long? value)
            {
                if (value is not null)
                {
                    sb.Append(key);
                    sb.Append('=');
                    sb.Append(value);
                    sb.Append(',');
                }
            }
        }
    }
}
