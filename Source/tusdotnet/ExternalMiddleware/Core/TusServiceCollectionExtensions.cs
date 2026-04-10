#if netstandard

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Models;

// ReSharper disable once CheckNamespace
namespace tusdotnet
{
    /// <summary>
    /// Extension methods for registering tusdotnet services in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class TusServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="ITusProtocolHandler"/> in the dependency injection container.
        /// The handler can be injected into controllers or other components to handle tus requests
        /// directly, without using middleware or endpoint routing.
        /// <para>
        /// Call <see cref="TusServiceCollectionBuilder.Configure(DefaultTusConfiguration)"/> on the
        /// returned builder to register a <see cref="DefaultTusConfiguration"/>, or register it
        /// yourself in the container — e.g. as scoped to get per-request configuration.
        /// </para>
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>A <see cref="TusServiceCollectionBuilder"/> to further configure tusdotnet.</returns>
        /// <example>
        /// Simple singleton setup:
        /// <code>
        /// builder.Services
        ///     .AddTus()
        ///     .Configure(new DefaultTusConfiguration
        ///     {
        ///         Store = new TusDiskStore("/tmp/uploads"),
        ///     });
        /// </code>
        /// Per-request (scoped) configuration pulling dependencies from DI:
        /// <code>
        /// builder.Services.AddScoped(sp => new DefaultTusConfiguration
        /// {
        ///     Store = sp.GetRequiredService&lt;ITusStore&gt;(),
        ///     Events = new Events { OnFileCompleteAsync = async ctx => { ... } }
        /// });
        /// builder.Services.AddTus();
        /// </code>
        /// </example>
        public static TusServiceCollectionBuilder AddTus(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddTransient<ITusProtocolHandler>(sp =>
                new TusProtocolHandler(sp.GetRequiredService<DefaultTusConfiguration>()));

            return new TusServiceCollectionBuilder(services);
        }
    }

    /// <summary>
    /// Builder returned by <see cref="TusServiceCollectionExtensions.AddTus"/> for further
    /// configuration of tusdotnet services.
    /// </summary>
    public sealed class TusServiceCollectionBuilder
    {
        private readonly IServiceCollection _services;

        internal TusServiceCollectionBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Registers the provided <see cref="DefaultTusConfiguration"/> as a singleton.
        /// </summary>
        public TusServiceCollectionBuilder Configure(DefaultTusConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _services.TryAddSingleton(configuration);
            return this;
        }

        /// <summary>
        /// Registers a <see cref="DefaultTusConfiguration"/> using the provided factory.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </summary>
        public TusServiceCollectionBuilder Configure(
            Func<IServiceProvider, DefaultTusConfiguration> factory,
            ServiceLifetime lifetime = ServiceLifetime.Singleton
        )
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _services.TryAdd(new ServiceDescriptor(typeof(DefaultTusConfiguration), factory, lifetime));
            return this;
        }
    }
}

#endif
