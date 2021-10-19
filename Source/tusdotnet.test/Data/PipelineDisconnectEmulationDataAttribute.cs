using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#if netfull
using System.Net;
using System.IO;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Internal.Networking;
#elif NETCOREAPP1_1
using System.IO;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Internal.Networking;
#elif NETCOREAPP2_0
using System.IO;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
#elif NETCOREAPP2_1
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
#endif

using Xunit.Sdk;

namespace tusdotnet.test.Data
{
    /// <summary>
    /// Data attribute to provide the different pipelines and helpers for PatchTests -> Handles_Abrupt_Disconnects_Gracefully
    /// CT indicates if CancellationToken.IsCancellationRequested is set or not
    /// Pipelines:
    /// System.Web.Host -> CT = true, Exception = Exception with message "Client disconnected"
    /// Microsoft.Owin.SelfHost -> CT = true, Exception = IOException, Exception.InnerException is System.Net.HttpListenerException
    /// .NET Core 1.1 reverse proxy IIS on ASP.NET Core 1.1 -> CT = true, Exception = Microsoft.AspNetCore.Server.Kestrel.Internal.Networking.UvException
    /// .NET Core 2.0 reverse proxy IIS on ASP.NET Core 2.0 -> CT = true, Exception = Microsoft.AspNetCore.Server.Kestrel.Internal.Networking.UvException
    /// .NET Core 2.1 reverse proxy IIS on ASP.NET Core 2.1 -> CT = true, Exception = Microsoft.AspNetCore.Connections.ConnectionResetException
    /// .NET Core 2.2 reverse proxy IIS on ASP.NET Core 2.2 -> CT = true, Exception = System.OperationCanceledException
    /// .NET Core 3.0 reverse proxy IIS on ASP.NET Core 3.0 -> CT = true, Exception = System.OperationCanceledException
    /// .NET Core 3.1 reverse proxy IIS on ASP.NET Core 3.1 -> CT = true, Exception = System.OperationCanceledException
    /// .NET 6 reverse proxy IIS on ASP.NET Core 6 -> CT = true, Exception = System.OperationCanceledException
    /// .NET Core 1.1 direct Kestrel on ASP.NET Core 1.1 -> CT = false, Exception = Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException
    /// .NET Core 2.0 direct Kestrel on ASP.NET Core 2.0 -> CT = true, Exception = Microsoft.AspNetCore.Server.Kestrel.BadHttpRequestException
    /// .NET Core 2.1 direct Kestrel on ASP.NET Core 2.1 -> CT = true, Exception = Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException
    /// .NET Core 2.2 direct Kestrel on ASP.NET Core 2.2 -> CT = true, Exception = System.OperationCanceledException
    /// .NET Core 3.0 direct Kestrel on ASP.NET Core 3.0 -> CT = true, Exception = System.OperationCanceledException
    /// .NET Core 3.1 direct Kestrel on ASP.NET Core 3.1 -> CT = true, Exception = System.OperationCanceledException
    /// .NET 6 direct Kestrel on ASP.NET Core 6 -> CT = true, Exception = System.IO.IOException
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

            return (DisconnectPipelineEmulationInfo)method.Invoke(null, null);
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
                .Select(f => new[] { f }).ToArray();
        }

#else

        private static object[][] GetPipelines()
        {
            return new object[]
                {
                    nameof(Kestrel),
                    nameof(KestrelReverseProxy)
                }
                .Select(f => new[] { f })
                .ToArray();
        }

#endif

        private static DisconnectPipelineEmulationInfo Kestrel()
        {
            // Request cancellation token is not flagged properly in ASP.NET Core 1.1, but it is in ASP.NET Core 2.0 and later.
#if NETCOREAPP1_1
            const bool properlyCancelsCancellationToken = false;
            var badHttpRequestExceptionCtorParams = new object[] { "", -1 };
#elif NETCOREAPP2_1
            const bool properlyCancelsCancellationToken = true;
            var badHttpRequestExceptionCtorParams = new object[] { "", -1, RequestRejectionReason.UnexpectedEndOfRequestContent };
#elif NETCOREAPP2_2_OR_GREATER && !NET6_0
            const bool properlyCancelsCancellationToken = true;
            var exception = new OperationCanceledException();
#elif NET6_0
            const bool properlyCancelsCancellationToken = true;
            var exception = new System.IO.IOException();
#else
            const bool properlyCancelsCancellationToken = true;
            var badHttpRequestExceptionCtorParams = new object[] { "", -1 };
#endif

            // NETCOREAPP2_2 and later does not create a BadHttpRequestException when a client disconnects
#if !NETCOREAPP2_2_OR_GREATER
            var ctor = typeof(BadHttpRequestException).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            var exception = (BadHttpRequestException)ctor.Invoke(badHttpRequestExceptionCtorParams);
#endif

            return new DisconnectPipelineEmulationInfo(properlyCancelsCancellationToken, exception);
        }

        private static DisconnectPipelineEmulationInfo KestrelReverseProxy()
        {
#if NETCOREAPP2_1
            var exceptionToThrow = new ConnectionResetException("Test");
#elif NETCOREAPP2_1_OR_GREATER
            var exceptionToThrow = new OperationCanceledException();
#else
            var exceptionToThrow = new IOException("Test", new UvException("Test", -4077));
#endif
            return new DisconnectPipelineEmulationInfo(true, exceptionToThrow);
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
