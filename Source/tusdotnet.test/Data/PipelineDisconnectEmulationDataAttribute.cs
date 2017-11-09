using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

#if netfull
using System.Net;
#else
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Internal.Networking;
#endif

using Xunit.Sdk;

namespace tusdotnet.test.Data
{
    /// <summary>
    /// Data attribute to provide the different pipelines and helpers for PatchTests -> Handles_Abrupt_Disconnects_Gracefully
    /// CT indicates if CancellationToken.IsCancellationRequested is set or not
    /// Pipelines: 
    /// System.Web.Host -> CT = true, Exception = "Client disconnected"
    /// Microsoft.Owin.SelfHost -> CT = true, Exception = IOException, Exception.InnerException is System.Net.HttpListenerException
    /// .NET Core reverse proxy IIS -> CT = true, Exception = Microsoft.AspNetCore.Server.Kestrel.Internal.Networking.UvException
    /// .NET Core direct Kestrel -> CT = false, Exception = Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class PipelineDisconnectEmulationDataAttribute : DataAttribute
    {
        public override IEnumerable<object[]> GetData(MethodInfo testMethod) => GetPipelines();

#if netfull

        public static DisconnectPipelineEmulationInfo GetInfo(string pipeline)
        {
            switch (pipeline)
            {
                case "System.Web":
                    return SystemWeb();
                case "Microsoft.Owin.SelfHost":
                    return OwinSelfHost();
                default:
                    throw new ArgumentException($"Unknown pipeline: {pipeline}", nameof(pipeline));
            }

            DisconnectPipelineEmulationInfo SystemWeb()
            {
                return new DisconnectPipelineEmulationInfo(true, new Exception("Client disconnected"));
            }

            DisconnectPipelineEmulationInfo OwinSelfHost()
            {
                return new DisconnectPipelineEmulationInfo(true, new IOException("Test", new HttpListenerException()));
            }
        }

        private static object[][] GetPipelines()
        {
            return new object[] { "System.Web", "Microsoft.Owin.SelfHost" }.Select(f => new[] { f }).ToArray();
        }

#else

        public static DisconnectPipelineEmulationInfo GetInfo(string pipeline)
        {
            switch (pipeline)
            {
                case "Microsoft.AspNetCore.Server.Kestrel":
                    return Kestrel();
                case "Microsoft.AspNetCore.Server.Kestrel_reverse_proxy":
                    return KestrelReverseProxy();
                default:
                    throw new ArgumentException($"Unknown pipeline: {pipeline}", nameof(pipeline));
            }

            DisconnectPipelineEmulationInfo Kestrel()
            {
                var ctor = typeof(BadHttpRequestException)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .First();
                var exception = (BadHttpRequestException)ctor.Invoke(new object[] { "", -1 });

                return new DisconnectPipelineEmulationInfo(false, exception);
            }

            DisconnectPipelineEmulationInfo KestrelReverseProxy()
            {
                return new DisconnectPipelineEmulationInfo(true,
                    new IOException("Test", new UvException("Test", -4077)));
            }
        }

        private static object[][] GetPipelines()
        {
            return new object[]
                       { "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Server.Kestrel_reverse_proxy" }
                .Select(f => new[] { f })
                .ToArray();
        }

#endif

        internal sealed class DisconnectPipelineEmulationInfo
        {
            public bool FlagsCancellationTokenAsCancelled { get; set; }

            public Exception ExceptionThatIsThrown { get; set; }

            public DisconnectPipelineEmulationInfo(bool flagsCancellationTokenAsCancelled, Exception exceptionThatIsThrown)
            {
                this.FlagsCancellationTokenAsCancelled = flagsCancellationTokenAsCancelled;
                ExceptionThatIsThrown = exceptionThatIsThrown;
            }
        }
    }
}
