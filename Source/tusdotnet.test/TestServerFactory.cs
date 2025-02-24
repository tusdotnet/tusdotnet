#if netstandard
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.test.Helpers;

#endif
#if netfull
using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Testing;
using Owin;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.test.Helpers;

#endif

namespace tusdotnet.test
{
    public static class TestServerFactory
    {
#if netstandard

        public static TestServer CreateWithForwarding(ITusStore store, Action onAuthorizeCalled, Action onForwarded, TusExtensions allowedExtensions = null)
        {
            return TestServerFactory.Create(app =>
            {
                app.UseTus(_ =>
                {
                    if (store == null)
                        return null;

                    return new DefaultTusConfiguration
                    {
                        Store = store,
                        UrlPath = "/files",
                        AllowedExtensions = allowedExtensions ?? TusExtensions.All,
                        Events = new Events
                        {
                            OnAuthorizeAsync = __ =>
                            {
                                onAuthorizeCalled();
                                return Task.FromResult(0);
                            }
                        }
                    };
                });

                app.Run(ctx =>
                {
                    onForwarded();
                    return TaskHelper.Completed;
                });
            });
        }


#if NET6_0_OR_GREATER

        public static TestServer Create(Action<IApplicationBuilder> startup)
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    startup(app);
                });

            return new TestServer(builder);
        }

        public static TestServer CreateWithFactory(Func<HttpContext, Task<DefaultTusConfiguration>> configurationFactory, string urlPath)
        {
            return Create(app => app.UseEndpoints(endpoints => endpoints.MapTus(urlPath, configurationFactory)));
        }

        public static TestServer Create(DefaultTusConfiguration config)
        {
            // Clear UrlPath as it's not used by endpoint routing.
            var urlPath = config.UrlPath;
            config.UrlPath = null;
            config.FileLockProvider ??= new TestServerInMemoryFileLockProvider();
            return Create(app => app.UseEndpoints(endpoints => endpoints.MapTus(urlPath, _ => Task.FromResult(config))));
        }

#else

        public static TestServer Create(Action<IApplicationBuilder> startup)
        {
            var host = new WebHostBuilder().Configure(startup);
            return new TestServer(host);
        }

        public static TestServer CreateWithFactory(Func<HttpContext, Task<DefaultTusConfiguration>> configurationFactory, string urlPath)
        {
            return Create(app => app.UseTus(configurationFactory));
        }

        public static TestServer Create(DefaultTusConfiguration config)
        {
            config.FileLockProvider ??= new TestServerInMemoryFileLockProvider();
            return Create(app => app.UseTus(_ => config));
        }

#endif

        public static TestServer Create(ITusStore store, Events events = null, MetadataParsingStrategy metadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues, TusExtensions allowedExtensions = null, bool usePipelinesIfAvailable = false)
        {
            return Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                Events = events,
                MetadataParsingStrategy = metadataParsingStrategy
#if pipelines
                ,
                UsePipelinesIfAvailable = usePipelinesIfAvailable
#endif
                ,
                AllowedExtensions = allowedExtensions ?? TusExtensions.All
            });
        }

#endif

#if netfull

        public static TestServer Create(Action<IAppBuilder> startup)
        {
            return TestServer.Create(startup);
        }

        public static TestServer Create(DefaultTusConfiguration config)
        {
            config.FileLockProvider ??= new TestServerInMemoryFileLockProvider();
            return Create(app => app.UseTus(_ => config));
        }

        public static TestServer CreateWithForwarding(ITusStore store, Action onAuthorizeCalled, Action onForwarded, TusExtensions allowedExtensions = null)
        {
            return Create(app =>
            {
                app.UseTus(_ =>
                {
                    if (store == null)
                        return null;

                    return new DefaultTusConfiguration
                    {
                        Store = store,
                        UrlPath = "/files",
                        AllowedExtensions = allowedExtensions ?? TusExtensions.All,
                        Events = new Events
                        {
                            OnAuthorizeAsync = __ =>
                            {
                                onAuthorizeCalled();
                                return Task.FromResult(0);
                            }
                        }
                    };
                });

                app.Run(ctx =>
                {
                    onForwarded();
                    return Task.FromResult(0);
                });
            });
        }

        public static TestServer CreateWithFactory(Func<IOwinRequest, Task<DefaultTusConfiguration>> configurationFactory, string urlPath)
        {
            return Create(app => app.UseTus(configurationFactory));
        }

        public static TestServer Create(ITusStore store, Events events = null, MetadataParsingStrategy metadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues, TusExtensions allowedExtensions = null)
        {
            return Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                Events = events,
                MetadataParsingStrategy = metadataParsingStrategy,
                AllowedExtensions = allowedExtensions ?? TusExtensions.All
            });
        }

#endif

    }
}
