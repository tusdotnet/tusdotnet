using System.Linq;
using NSubstitute;
using NSubstitute.Core;

namespace tusdotnet.test.Extensions
{
    internal static class SubstituteExtensions
    {
        internal static ICall GetSingleMethodCall<T>(this T substitute, string methodName) where T : class
        {
            return substitute.ReceivedCalls().SingleOrDefault(f => f.GetMethodInfo().Name == methodName);
        }
    }
}
