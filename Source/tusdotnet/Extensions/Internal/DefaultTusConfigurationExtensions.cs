using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Extensions.Internal
{
    internal static class DefaultTusConfigurationExtensions
    {
        internal static bool SupportsClientTag(this DefaultTusConfiguration config)
        {
            return config.Store is ITusClientTagStore && config.Events?.OnResolveClientTagAsync != null;
        }
    }
}
