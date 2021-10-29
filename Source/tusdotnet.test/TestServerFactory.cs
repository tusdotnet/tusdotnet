#if netstandard
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.test.Helpers;

#endif
#if netfull
using System;
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

        public static TestServer Create(Action<IApplicationBuilder> startup)
        {
            var host = new WebHostBuilder().Configure(startup);
            return new TestServer(host);
        }

        public static TestServer Create(DefaultTusConfiguration config)
        {
            config.FileLockProvider ??= new TestServerInMemoryFileLockProvider();
            return Create(app => app.UseTus(_ => config));
        }

        public static TestServer Create(ITusStore store, Events events = null, MetadataParsingStrategy metadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues, bool usePipelinesIfAvailable = false)
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

        public static TestServer Create(ITusStore store, Events events = null, MetadataParsingStrategy metadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues)
        {
            return Create(new DefaultTusConfiguration
            {
                UrlPath = "/files",
                Store = store,
                Events = events,
                MetadataParsingStrategy = metadataParsingStrategy
            });
        }

#endif

    }
}
