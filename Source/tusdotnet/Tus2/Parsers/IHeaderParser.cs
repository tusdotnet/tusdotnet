using Microsoft.AspNetCore.Http;

namespace tusdotnet.Tus2
{
    public interface IHeaderParser
    {
        Tus2Headers Parse(HttpContext httpContext);
    }
}
