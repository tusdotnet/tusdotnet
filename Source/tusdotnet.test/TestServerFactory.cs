#if netstandard
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using tusdotnet.Interfaces;
using tusdotnet.Models;

#endif
#if netfull
using System;
using Microsoft.Owin.Testing;
using Owin;
using tusdotnet.Interfaces;
using tusdotnet.Models;

#endif

namespace tusdotnet.test
{
    public static class TestServerFactory
    {

#if netstandard

        public static TestServer Create(Action<IApplicationBuilder> startup)
        {
            var host = new WebHostBuilder().UseStartup<EmptyStartup>().Configure(startup);
            return new TestServer(host);
        }

        public static TestServer Create(ITusStore store)
        {
            return Create(app =>
            {
                app.UseTus(context => new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store
                });
            });
        }

#endif

#if netfull

	    public static TestServer Create(Action<IAppBuilder> startup)
	    {
		    return TestServer.Create(startup);
	    }

        public static TestServer Create(ITusStore store)
        {
            return Create(app =>
            {
                app.UseTus(context => new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = store
                });
            });
        }

#endif

        // ReSharper disable once UnusedMember.Local
        private class EmptyStartup
        {
            // Left blank
        }
    }
}
