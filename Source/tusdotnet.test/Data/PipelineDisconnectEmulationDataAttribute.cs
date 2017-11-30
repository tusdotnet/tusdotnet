using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

#if netfull
using System.Net;
#elif NETCOREAPP2_0
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;
#endif

#if !NETCOREAPP2_0
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
    /// .NET Core direct Kestrel on ASP.NET Core 1.1 -> CT = false, Exception = Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException
    /// .NET Core direct Kestrel on ASP.NET Core 2.0 -> CT = true, Exception = Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class PipelineDisconnectEmulationDataAttribute : DataAttribute
    {
        public override IEnumerable<object[]> GetData(MethodInfo testMethod) => GetPipelines();

        private static readonly Lazy<Dictionary<string, MethodInfo>> Methods = new Lazy<Dictionary<string, MethodInfo>>(
            () =>
            {
                return typeof(PipelineDisconnectEmulationDataAttribute)
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(f => f.ReturnType == typeof(DisconnectPipelineEmulationInfo))
                    .ToDictionary(f => f.Name, f => f);
            });

        public static DisconnectPipelineEmulationInfo GetInfo(string pipeline)
        {
            if (!Methods.Value.TryGetValue(pipeline, out var method))
            {
                throw new ArgumentException($"Unknown pipeline: {pipeline}", nameof(pipeline));
            }

            return (DisconnectPipelineEmulationInfo) method.Invoke(null, null);
        }

#if netfull

        private static object[][] GetPipelines()
        {
            return new object[]
                {
                    nameof(SystemWebWithOwin),
                    nameof(OwinSelfHost),
                    nameof(Kestrel),
                    nameof(KestrelReverseProxy)
                }
                .Select(f => new[] {f}).ToArray();
        }

#else

        private static object[][] GetPipelines()
        {
            return new object[]
                {
                    nameof(Kestrel),
                    nameof(KestrelReverseProxy)
                }
                .Select(f => new[] {f})
                .ToArray();
        }

#endif

        private static DisconnectPipelineEmulationInfo Kestrel()
        {
            // Request cancellation token is not flagged properly in ASP.NET Core 1.1, but it is in ASP.NET Core 2.0.
            // netfull uses ASP.NET Core 2.0, hence the ifdef.
#if NETCOREAPP2_0 || netfull
            const bool properlyCancelsCancellationToken = true;
#else
            const bool properlyCancelsCancellationToken = false;
#endif

            var ctor = typeof(BadHttpRequestException)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .First();
            var exception = (BadHttpRequestException)ctor.Invoke(new object[] { "", -1 });

            return new DisconnectPipelineEmulationInfo(properlyCancelsCancellationToken, exception);
        }

        private static DisconnectPipelineEmulationInfo KestrelReverseProxy()
        {
            return new DisconnectPipelineEmulationInfo(true,
                new IOException("Test", new UvException("Test", -4077)));
        }

#if netfull

        private static DisconnectPipelineEmulationInfo SystemWebWithOwin()
        {
            return new DisconnectPipelineEmulationInfo(true, new Exception("Client disconnected"));
        }

        private static DisconnectPipelineEmulationInfo OwinSelfHost()
        {
            return new DisconnectPipelineEmulationInfo(true, new IOException("Test", new HttpListenerException()));
        }

#endif
    }
}
