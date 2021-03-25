using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using tusdotnet.ExternalMiddleware.EndpointRouting;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    // Added as scoped so one could cache the same configuration each time if desirable to save GC pressure.
    public class MyTusConfigurator : ITusConfigurator
    {
        public Task<EndpointOptions> Configure(HttpContext context)
        {
            return Task.FromResult(new EndpointOptions
            {
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(10)),
                Store = new TusDiskStore(@"C:\tusfiles\")
            });
        }
    }
}
