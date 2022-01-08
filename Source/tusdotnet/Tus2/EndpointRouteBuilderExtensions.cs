using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public static class EndpointRouteBuilderExtensions
    {
        public static IEndpointRouteBuilder MapTus2<T>(this IEndpointRouteBuilder endpoints, string route) where T : TusBaseHandler
        {
            endpoints.Map(route, Tus2Endpoint.Invoke<T>);

            return endpoints;
        }

    }
}
