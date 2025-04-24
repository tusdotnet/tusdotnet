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
                AllowAnyHeader = true,
                AllowAnyMethod = true,
                AllowAnyOrigin = true,
            };

            corsPolicy
                .GetType()
                .GetProperty(nameof(corsPolicy.ExposedHeaders))
                .SetValue(corsPolicy, CorsHelper.GetExposedHeaders());

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
