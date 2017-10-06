#if netfull

using System;
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
        public static void UseTus(this IAppBuilder builder, Func<IOwinRequest, DefaultTusConfiguration> configFactory)
        {
            builder.Use<TusOwinMiddleware>(configFactory);
        }
    }
}

#endif