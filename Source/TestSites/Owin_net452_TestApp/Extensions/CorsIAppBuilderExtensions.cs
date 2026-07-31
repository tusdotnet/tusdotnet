using System.Threading.Tasks;
using Microsoft.Owin.Cors;
using Owin;
using tusdotnet.Helpers;

namespace Owin_net452_TestApp.Extensions
{
    public static class CorsIAppBuilderExtensions
    {
        public static void SetupCorsPolicy(this IAppBuilder app)
        {
            var corsPolicy = new System.Web.Cors.CorsPolicy
            {
                AllowAnyHeader = false,
                AllowAnyMethod = false,
                AllowAnyOrigin = true,
            };

            foreach (var header in CorsHelper.GetAllowedHeaders())
                corsPolicy.Headers.Add(header);

            foreach (var method in CorsHelper.GetAllowedMethods())
                corsPolicy.Methods.Add(method);

            foreach (var header in CorsHelper.GetExposedHeaders())
                corsPolicy.ExposedHeaders.Add(header);

            app.UseCors(
                new CorsOptions
                {
                    PolicyProvider = new CorsPolicyProvider
                    {
                        PolicyResolver = context => Task.FromResult(corsPolicy),
                    },
                }
            );
        }
    }
}
