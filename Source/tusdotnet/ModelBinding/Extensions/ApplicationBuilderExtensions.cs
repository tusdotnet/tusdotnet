#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
using tusdotnet.ModelBinding.Middlewares;

namespace tusdotnet.ModelBinding.ModelBinders
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseResumableUploadModelBinding(
            this IApplicationBuilder app,
            bool consolidateRequests
        )
        {
            if (!consolidateRequests)
            {
                return app;
            }

            return app.UseMiddleware<ResumableUploadModelBindingWithConsolidateRequestsMiddleware>();
        }
    }
}
#endif
