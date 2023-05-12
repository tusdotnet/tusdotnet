#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Builder;

namespace tusdotnet.ModelBinders
{
    public static class ResumableUploadModelBindingMiddlewareExtensions
    {
        public static IApplicationBuilder UseResumableUploadModelBinding(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ResumableUploadModelBindingMiddleware>();
        }
    }

}
#endif