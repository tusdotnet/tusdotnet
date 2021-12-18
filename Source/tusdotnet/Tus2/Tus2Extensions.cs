using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal static class Tus2Extensions
    {
        internal static async Task Error(this HttpContext httpContext, HttpStatusCode statusCode, string message = null)
        {
            httpContext.Response.StatusCode = (int)statusCode;
            if (message != null)
                await httpContext.Response.WriteAsync(message);
        }

        internal static void SetHeader(this HttpContext context, string key, string value)
        {
            context.Response.Headers[key] = new StringValues(value);
        }

        internal static string DataFilePath(this Tus2Options options, string uploadToken)
        {
            return Path.Combine(options.FolderDiskPath, uploadToken);
        }

        internal static string MetadataFilePath(this Tus2Options options, string uploadToken)
        {
            return Path.Combine(options.FolderDiskPath, uploadToken) + ".metadata";
        }

        internal static string CompletedFilePath(this Tus2Options options, string uploadToken)
        {
            return Path.Combine(options.FolderDiskPath, uploadToken) + ".completed";
        }

        internal static string OngoingFilePath(this Tus2Options options, string uploadToken)
        {
            return Path.Combine(options.FolderDiskPath, uploadToken) + ".ongoing";
        }

        internal static string CancelFilePath(this Tus2Options options, string uploadToken)
        {
            return Path.Combine(options.FolderDiskPath, uploadToken) + ".cancel";
        }
    }
}
