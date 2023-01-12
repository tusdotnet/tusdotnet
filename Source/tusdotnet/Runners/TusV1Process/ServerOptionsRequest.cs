#if NET6_0_OR_GREATER

using tusdotnet.Adapters;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public class ServerOptionsRequest : TusV1Request
    {
        internal ContextAdapter ToContextAdapter(DefaultTusConfiguration config)
        {
            return ToContextAdapter("options", config);
        }

        internal static ServerOptionsRequest FromContextAdapter(ContextAdapter context)
        {
            return new()
            {
                CancellationToken = context.CancellationToken
            };
        }
    }
}

#endif