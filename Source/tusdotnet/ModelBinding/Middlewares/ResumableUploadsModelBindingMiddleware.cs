#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using tusdotnet.ModelBinding.ModelBinders;
using tusdotnet.ModelBinding.ProtocolHandler;

namespace tusdotnet.ModelBinding.Middlewares
{
    internal class ResumableUploadModelBindingWithConsolidateRequestsMiddleware
    {
        private readonly RequestDelegate _next;

        public ResumableUploadModelBindingWithConsolidateRequestsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var file = await ModelBindingHandler.BindFromHttpContext(httpContext);

            if (file is not null)
            {
                httpContext.Features.Set(new UploadCompleteFeature(file));
                await _next(httpContext);
            }
        }
    }
}
#endif
