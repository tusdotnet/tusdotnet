#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Runtime.CompilerServices;

namespace tusdotnet.Extensions.Internal
{
    internal static class ReadOnlySpanExtensions
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (bool Success, byte[] DecodedValue) TryDecodeBase64(this ReadOnlySpan<char> value)
        {
            var bytes = new byte[value.GetBase64ByteLength()];
            return (Convert.TryFromBase64Chars(value, bytes, out var _), bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBase64ByteLength(this ReadOnlySpan<char> value)
        {
            var numberOfPaddingCharacters = value.CountNumberOfBase64PaddingCharacters();
            var byteLength = (3 * (value.Length / 4)) - numberOfPaddingCharacters;
            return byteLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountNumberOfBase64PaddingCharacters(this ReadOnlySpan<char> value)
        {
            var numberOfPaddingCharacters = 0;

            for (var i = value.Length - 1; i > 0; i--)
            {
                if (value[i] != '=')
                    break;

                numberOfPaddingCharacters++;
            }

            return numberOfPaddingCharacters;
        }
    }
}

#endif