#if netstandard
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
#endif
#if netfull
using System;
using Microsoft.Owin.Testing;
using Owin;
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
	#endif

	#if netfull

	    public static TestServer Create(Action<IAppBuilder> startup)
	    {
		    return TestServer.Create(startup);
	    }

	#endif

	    // ReSharper disable once ClassNeverInstantiated.Local
	    private class EmptyStartup
	    {
		    // Left blank
	    }
	}
}
