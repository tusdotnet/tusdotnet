using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public static class Tus2InfoEndpoint
    {
        public static async Task Invoke(HttpContext httpContext)
        {
            var options = httpContext.RequestServices.GetRequiredService<IOptions<Tus2Options>>().Value;

            var headers = Tus2Headers.Parse(httpContext);

            headers.UploadToken = Tus2Validator.CleanUploadToken(headers.UploadToken);

            if (string.IsNullOrWhiteSpace(headers.UploadToken))
            {
                await httpContext.Error(System.Net.HttpStatusCode.BadRequest, "Missing Upload-Token header");
            }

            var path = options.DataFilePath(headers.UploadToken);
            var exists = File.Exists(path);
            var fileSize = exists ? (long?)new FileInfo(path).Length : null;
            var isComplete = File.Exists(options.CompletedFilePath(headers.UploadToken));

            var sb = new StringBuilder();
            sb.AppendFormat("Exists: {0}\n", exists);
            sb.AppendFormat("Size: {0}\n", fileSize?.ToString() ?? "<null>");
            sb.AppendFormat("IsComplete: {0}\n", isComplete);

            httpContext.Response.StatusCode = 200;
            await httpContext.Response.WriteAsync(sb.ToString());
        }
    }
}
