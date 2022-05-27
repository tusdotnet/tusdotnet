using System;
using tusdotnet.Models;

namespace tusdotnet.Adapters
{
    internal class EndpointConfigurationValidator : IConfigurationValidator
    {
        public static EndpointConfigurationValidator Instance { get; } = new();

        private EndpointConfigurationValidator()
        {
        }

        public void Validate(DefaultTusConfiguration configuration)
        {
            if (configuration.UrlPath != null)
            {
                throw new TusConfigurationException("UrlPath cannot be set when used from endpoint routing.");
            }

            if (configuration.Store == null)
            {
                throw new TusConfigurationException($"{nameof(configuration.Store)} cannot be null.");
            }

            if (!Enum.IsDefined(typeof(MetadataParsingStrategy), configuration.MetadataParsingStrategy))
            {
                throw new TusConfigurationException($"{nameof(MetadataParsingStrategy)} is not a valid value.");
            }
        }
    }
}
