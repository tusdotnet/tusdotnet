#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Runtime.CompilerServices;

namespace tusdotnet.Extensions.Internal
{
    internal static class ReadOnlySpanExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Count(this ReadOnlySpan<char> span, char characterToFind)
        {
            var numberOfCharacters = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == characterToFind)
                {
                    numberOfCharacters++;
                }
            }

            return numberOfCharacters;
        }
    }
}

#endif