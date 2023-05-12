#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace tusdotnet.ModelBinders
{
    internal static class GenericModelBinder
    {
        internal static async Task<ResumableUpload> BindFromHttpContext(HttpContext httpContext)
        {
            if (httpContext.Features[typeof(ResumableUploadCompleteFeature)] is not ResumableUploadCompleteFeature feature)
                return null;

            var contentTask = feature.File.GetContentAsync(default);
            var metadataTask = feature.File.GetMetadataAsync(default);

            await Task.WhenAll(contentTask, metadataTask);

            var upload = new ResumableUpload(feature.File.Id, contentTask.Result, metadataTask.Result);
            return upload;
        }
    }
}

#endif