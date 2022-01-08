using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal static class Tus2Endpoint
    {
        internal static async Task Invoke<T>(HttpContext httpContext) where T : TusBaseHandler
        {
            if (httpContext.Request.Method.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                await httpContext.Error(HttpStatusCode.MethodNotAllowed);
                return;
            }

            var options = httpContext.RequestServices.GetRequiredService<IOptions<Tus2Options>>();
            var store = new Tus2DiskStore(options.Value);
            var ongoingUploadService = new UploadManagerDiskBased(options.Value);
            var headers = Tus2Headers.Parse(httpContext);

            if (string.IsNullOrWhiteSpace(headers.UploadToken))
            {
                await httpContext.Error(HttpStatusCode.BadRequest, "Missing Upload-Token header");
                return;
            }

            var tusContext = new EndpointContext(store, headers, httpContext);
            var controller = CreateController<T>(httpContext, options, ongoingUploadService, tusContext);

            Tus2BaseResponse response = null;

            try
            {
                response = await InvokeController(httpContext, controller);
            }
            finally
            {
                if (response != null)
                {
                    await response.WriteTo(httpContext);
                }
                else
                {
                    await httpContext.Error(HttpStatusCode.InternalServerError);
                }
            }
        }

        private static async Task<Tus2BaseResponse> InvokeController(HttpContext httpContext, TusBaseHandlerEntryPoints controller)
        {
            var method = httpContext.Request.Method;

            if (method.Equals("head", StringComparison.OrdinalIgnoreCase))
            {
                return await controller.RetrieveOffsetEntryPoint();
            }
            else if (method.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                return await controller.DeleteEntryPoint();
            }

            return await controller.WriteDataEntryPoint();
        }

        private static T CreateController<T>(HttpContext httpContext, IOptions<Tus2Options> options, UploadManagerDiskBased ongoingUploadService, EndpointContext tusContext) where T : TusBaseHandler
        {
            var metadataParser = httpContext.RequestServices.GetRequiredService<IMetadataParser>();

            var controller = httpContext.RequestServices.GetRequiredService<T>();
            controller.TusContext = tusContext;
            controller.MetadataParser = metadataParser;
            controller.UploadManager = ongoingUploadService;
            controller.AllowClientToDeleteFile = options.Value.AllowClientToDeleteFile;
            return controller;
        }
    }
}