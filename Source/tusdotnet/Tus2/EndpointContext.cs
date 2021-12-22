using Microsoft.AspNetCore.Http;

namespace tusdotnet.Tus2
{
    public record EndpointContext(
        Tus2DiskStore Store, 
        Tus2Headers Headers, 
        HttpContext HttpContext);
}
