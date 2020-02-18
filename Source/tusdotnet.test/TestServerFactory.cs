#if netstandard
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

#endif
#if netfull
using System;
using Microsoft.Owin.Testing;
using Owin;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

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

        public static TestServer Create(ITusStore store, Events events = null, MetadataParsingStrategy metadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues)
        {
            return Create(app =>
            {
                app.UseTus(_ => new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    Events = events,
                    MetadataParsingStrategy = metadataParsingStrategy
                });
            });
        }

#endif

#if netfull

	    public static TestServer Create(Action<IAppBuilder> startup)
	    {
		    return TestServer.Create(startup);
	    }

        public static TestServer Create(ITusStore store, Events events = null, MetadataParsingStrategy metadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues)
        {
            return Create(app =>
            {
                app.UseTus(_ => new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store,
                    Events = events,
                    MetadataParsingStrategy = metadataParsingStrategy
                });
            });
        }

#endif

    }
}
