using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal static class Tus2StatusEndpoint
    {
        public static async Task Invoke(string fileId, HttpContext httpContext)
        {
            await httpContext.Response.WriteAsJsonAsync(new
            {
                FileId = fileId,
                SelfUrl = httpContext.Request.GetDisplayUrl(),
                Status = "Success"
            });
        }
    }
}
