using System;
using System.Security.Claims;
using System.Text;
using Owin;

namespace Owin_net452_TestApp.Extensions
{
    public static class BasicAuthenticationIAppBuilderExtensions
    {
        public static void SetupSimpleBasicAuth(this IAppBuilder app)
        {
            // Note: Just a very simple basic auth to show how OnAuthorizeAsync can work with OWIN. Do NOT use this authentication method in production.
            app.Use(
                async (context, next) =>
                {
                    const string Username = "test";
                    const string Password = "test";
                    const string AuthorizationHeader = "Authorization";
                    const string BasicSchemeName = "Basic";

                    if (context.Authentication.User?.Identity?.IsAuthenticated == true)
                    {
                        await next();
                        return;
                    }

                    var basicAuthHeader = context.Request.Headers[AuthorizationHeader];
                    if (basicAuthHeader == null)
                    {
                        await next();
                        return;
                    }

                    var parts = Encoding
                        .UTF8.GetString(
                            Convert.FromBase64String(
                                basicAuthHeader.Substring(BasicSchemeName.Length)
                            )
                        )
                        .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        context.Response.StatusCode = 400;
                        return;
                    }

                    if (parts[0] == Username && parts[1] == Password)
                    {
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, Username),
                            new Claim(ClaimTypes.Name, Username)
                        };
                        context.Authentication.User = new ClaimsPrincipal(
                            new ClaimsIdentity(claims, BasicSchemeName)
                        );
                        await next();
                        return;
                    }

                    context.Response.StatusCode = 401;
                }
            );
        }
    }
}
