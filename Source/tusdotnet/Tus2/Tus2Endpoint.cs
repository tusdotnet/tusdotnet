#nullable enable
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Tus2.Parsers;

namespace tusdotnet.Tus2
{
    internal static class Tus2Endpoint
    {
        internal static async Task Invoke<T>(HttpContext httpContext, EndpointConfiguration? configuration = null) where T : TusHandler
        {
            // TODO Remove this and just use the MapX methods on the endpoint builder one step up.
            if (httpContext.Request.Method.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                await httpContext.Error(HttpStatusCode.MethodNotAllowed);
                return;
            }

            var headers = Tus2HeadersParser.Parse(httpContext);

            if (string.IsNullOrWhiteSpace(headers.UploadToken))
            {
                await httpContext.Error(HttpStatusCode.BadRequest, "Missing Upload-Token header");
                return;
            }


            Tus2BaseResponse? response = null;
            configuration ??= new EndpointConfiguration(null);

            try
            {
                var handler = await CreateHandler<T>(httpContext, configuration, headers);
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

        private static async Task<Tus2BaseResponse> InvokeHandler(TusBaseHandlerEntryPoints handler)
        {
            var method = handler.HttpContext.Request.Method;

            if (method.Equals("head", StringComparison.OrdinalIgnoreCase))
            {
                return await handler.RetrieveOffsetEntryPoint();
            }
            else if (method.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                return await handler.DeleteEntryPoint();
            }

            var writeResponse = await handler.WriteDataEntryPoint();

            if (!writeResponse.IsError && writeResponse.UploadCompleted)
            {
                await handler.OnFileComplete();
            }

            return writeResponse;
        }

        private static async Task<T> CreateHandler<T>(HttpContext httpContext, EndpointConfiguration configuration, Tus2Headers headers) where T : TusHandler
        {
            var handler = httpContext.RequestServices.GetRequiredService<T>();

            // MetadataParser might not be required?
            var metadataParser = httpContext.RequestServices.GetRequiredService<IMetadataParser>();
            var configurationManager = httpContext.RequestServices.GetRequiredService<ITus2ConfigurationManager>();

            var store = await GetStore(configurationManager, configuration.StorageConfigurationName);
            var uploadManager = await GetUploadManager(configurationManager, configuration.UploadManagerConfigurationName);

            handler.UploadManager = uploadManager;
            handler.Store = store; ;
            handler.MetadataParser = metadataParser;
            handler.AllowClientToDeleteFile = configuration.AllowClientToDeleteFile ?? false;
            handler.Headers = headers;
            handler.HttpContext = httpContext;

            return handler;

            static async Task<ITus2Storage> GetStore(ITus2ConfigurationManager manager, string configurationName)
            {
                return configurationName == null
                    ? await manager.GetDefaultStorage()
                    : await manager.GetNamedStorage(configurationName);
            }

            static async Task<IUploadManager> GetUploadManager(ITus2ConfigurationManager manager, string configurationName)
            {
                return configurationName == null
                    ? await manager.GetDefaultUploadManager()
                    : await manager.GetNamedUploadManager(configurationName);
            }
        }
    }
}