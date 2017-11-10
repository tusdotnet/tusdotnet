#if netfull

using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    /// <summary>
    /// Extension methods for adding tusdotnet to an application pipeline.
    /// </summary>
    public static class TusAppBuilderExtensions
    {
        /// <summary>
        /// Adds the tusdotnet middleware to your web application pipeline.
        /// </summary>
        /// <param name="builder">The IAppBuilder passed to your configuration method</param>
        /// <param name="configFactory">Factory for creating the configuration for tusdotnet</param>
        public static IAppBuilder UseTus(this IAppBuilder builder, Func<IOwinRequest, DefaultTusConfiguration> configFactory)
        {
            return builder.UseTus((Func<IOwinRequest, Task<DefaultTusConfiguration>>)AsyncFactory);

            Task<DefaultTusConfiguration> AsyncFactory(IOwinRequest ctx) => Task.FromResult(configFactory(ctx));
        }

        /// <summary>
        /// Adds the tusdotnet middleware to your web application pipeline.
        /// </summary>
        /// <param name="builder">The IAppBuilder passed to your configuration method</param>
        /// <param name="configFactory">Factory for creating the configuration for tusdotnet</param>
        public static IAppBuilder UseTus(this IAppBuilder builder, Func<IOwinRequest, Task<DefaultTusConfiguration>> configFactory)
        {
            return builder.Use<TusOwinMiddleware>(configFactory);
        }
    }
}

#endif