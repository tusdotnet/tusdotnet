using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Extensions
{
    // ReSharper disable once InconsistentNaming
    internal static class ITusConfigurationExtensions
    {
        public static void Validate(this ITusConfiguration config)
        {
            if (config.Store == null)
            {
                throw new TusConfigurationException($"{nameof(config.Store)} cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(config.UrlPath))
            {
                throw new TusConfigurationException($"{nameof(config.UrlPath)} cannot be empty.");
            }
        }
    }
}
