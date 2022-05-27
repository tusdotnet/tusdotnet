using tusdotnet.Models;

namespace tusdotnet.Adapters
{
    internal interface IConfigurationValidator
    {
        void Validate(DefaultTusConfiguration configuration);
    }
}