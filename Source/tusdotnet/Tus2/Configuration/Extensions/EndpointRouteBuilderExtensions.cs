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
        public static IEndpointRouteBuilder MapTus2<T>(this IEndpointRouteBuilder endpoints, string route, EndpointConfiguration configuration) where T : TusHandler
        {
            endpoints.Map(route, async httpContext =>
            {
                await Tus2Endpoint.Invoke<T>(httpContext, configuration);
            });

            return endpoints;
        }

    }
}
