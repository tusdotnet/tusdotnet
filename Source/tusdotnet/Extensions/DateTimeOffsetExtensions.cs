using System;

namespace tusdotnet.Extensions
{
    internal static class DateTimeOffsetExtensions
    {
        internal static bool HasPassed(this DateTimeOffset dateTime)
        {
            return dateTime.ToUniversalTime().CompareTo(DateTimeOffset.UtcNow) == -1;
        }
    }
}
