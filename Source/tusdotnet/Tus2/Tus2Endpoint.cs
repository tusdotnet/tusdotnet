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
        internal static async Task Invoke<T>(HttpContext httpContext, EndpointConfiguration configuration) where T : TusHandler
        {
            if (httpContext.Request.Method.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                await httpContext.Error(HttpStatusCode.MethodNotAllowed);
                return;
            }

            var headers = Tus2Headers.Parse(httpContext);

            if (string.IsNullOrWhiteSpace(headers.UploadToken))
            {
                await httpContext.Error(HttpStatusCode.BadRequest, "Missing Upload-Token header");
                return;
            }

            var metadataParser = httpContext.RequestServices.GetRequiredService<IMetadataParser>();
            var configurationManager = httpContext.RequestServices.GetService<ITus2ConfigurationManager>();
            var store = await configurationManager.GetStore(configuration.StorageConfigurationName);
            var uploadManager = await configurationManager.GetUploadManager(configuration.UploadManagerConfigurationName);
            var options = GetOptions(httpContext, configuration);

            var handler = CreateHandler<T>(uploadManager, new TusHandlerContext(store, metadataParser, options.AllowClientToDeleteFile, headers, httpContext));

            Tus2BaseResponse response = null;

            try
            {
                response = await InvokeHandler(handler);
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

        private static Tus2Options GetOptions(HttpContext httpContext, EndpointConfiguration configuration)
        {
            if (configuration.AllowClientToDeleteFile != null)
                return new() { AllowClientToDeleteFile = configuration.AllowClientToDeleteFile.Value };

            return httpContext.RequestServices.GetRequiredService<IOptions<Tus2Options>>().Value;
        }

        private static async Task<Tus2BaseResponse> InvokeHandler(TusBaseHandlerEntryPoints handler)
        {
            var method = handler.TusContext.HttpContext.Request.Method;

            if (method.Equals("head", StringComparison.OrdinalIgnoreCase))
            {
                return await handler.RetrieveOffsetEntryPoint();
            }
            else if (method.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                return await handler.DeleteEntryPoint();
            }

            return await handler.WriteDataEntryPoint();
        }

        private static T CreateHandler<T>(IUploadManager uploadManager, TusHandlerContext tusContext) where T : TusHandler
        {
            var handler = tusContext.HttpContext.RequestServices.GetRequiredService<T>();
            handler.UploadManager = uploadManager;
            handler.TusContext = tusContext;
            return handler;
        }
    }
}