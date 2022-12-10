using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace AspNetCore_net6._0_TestApp.Authentication;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    // Don't do this in production...
    private const string Username = "test";
    private const string Password = "test";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IHttpContextAccessor httpContextAccessor)
        : base(options, logger, encoder, clock)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var shouldAuthorize = HasAuthorizeAttribute();

        if(!shouldAuthorize)
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.ContainsKey("Authorization"))
        {
            // Force browser to display login prompt.
            _httpContextAccessor.HttpContext!.Response.Headers.Add("WWW-Authenticate", new StringValues("Basic realm=tusdotnet-test-net6"));
            return Task.FromResult(AuthenticateResult.Fail("No header provided"));
        }

        bool isAuthenticated;
        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter!)).Split(':');
            isAuthenticated = Authenticate(credentials[0], credentials[1]);
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }

        if (!isAuthenticated)
            return Task.FromResult(AuthenticateResult.Fail("Invalid Username or Password"));

        var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, Username),
                new Claim(ClaimTypes.Name, Username),
            };

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }

    private bool HasAuthorizeAttribute()
    {
        return Context.GetEndpoint()?.Metadata.Any(x => x is AuthorizeAttribute) == true;
    }

    private static bool Authenticate(string username, string password)
    {
        return username == Username && password == Password;
    }
}
