using System;
using tusdotnet.Models;

namespace tusdotnet.Adapters
{
    internal class MiddlewareConfigurationValidator : IConfigurationValidator
    {
        public static MiddlewareConfigurationValidator Instance { get; } = new();

        private MiddlewareConfigurationValidator()
        {
        }

        public void Validate(DefaultTusConfiguration configuration)
        {
            if (configuration.Store == null)
            {
                throw new TusConfigurationException($"{nameof(configuration.Store)} cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(configuration.UrlPath))
            {
                throw new TusConfigurationException($"{nameof(configuration.UrlPath)} cannot be empty.");
            }

            if (!Enum.IsDefined(typeof(MetadataParsingStrategy), configuration.MetadataParsingStrategy))
            {
                throw new TusConfigurationException($"{nameof(MetadataParsingStrategy)} is not a valid value.");
            }
        }
    }
}
