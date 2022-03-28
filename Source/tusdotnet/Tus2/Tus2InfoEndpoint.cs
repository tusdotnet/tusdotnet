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

            var headers = new Tus2HeadersParser().Parse(httpContext);

            if (string.IsNullOrWhiteSpace(headers.UploadToken))
            {
                await httpContext.Error(System.Net.HttpStatusCode.BadRequest, "Missing Upload-Token header");
            }

            headers.UploadToken = Tus2DiskStorage.CleanUploadToken(headers.UploadToken);

            var pathHelper = new DiskPathHelper(options.FolderDiskPath);
            var path = pathHelper.DataFilePath(headers.UploadToken);
            var exists = File.Exists(path);
            var fileSize = exists ? (long?)new FileInfo(path).Length : null;
            var isComplete = File.Exists(pathHelper.CompletedFilePath(headers.UploadToken));
            var metadata = File.Exists(pathHelper.MetadataFilePath(headers.UploadToken)) ? File.ReadAllText(pathHelper.MetadataFilePath(headers.UploadToken)) : null;

            var sb = new StringBuilder();
            sb.AppendFormat("Exists: {0}\n", exists);
            sb.AppendFormat("Size: {0}\n", fileSize?.ToString() ?? "<null>");
            sb.AppendFormat("IsComplete: {0}\n", isComplete);
            sb.AppendFormat("Metadata: {0}\n", metadata);

            httpContext.Response.StatusCode = 200;
            await httpContext.Response.WriteAsync(sb.ToString());
        }
    }
}
