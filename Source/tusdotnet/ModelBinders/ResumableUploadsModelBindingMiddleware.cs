#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using tusdotnet.Adapters;
using tusdotnet.Helpers;
using tusdotnet.Interfaces;
using tusdotnet.ModelBinders.Validation;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.ModelBinders
{
    internal class ResumableUploadModelBindingMiddleware
    {
        private readonly RequestDelegate _next;

        private const string TUSDOTNET_UPLOAD_ID_QUERY_NAME = "tdn-upload-id";

        public ResumableUploadModelBindingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var endpoint = httpContext.GetEndpoint();

            // TODO: These needs to be options
            const bool BIND_ANY_TYPE = true;
            Func<Dictionary<string, Metadata>, string> resolveContentType = meta =>
            {
                if (meta.TryGetValue("contentType", out var contentType))
                    return contentType.GetString(System.Text.Encoding.UTF8);

                return string.Empty;
            };

            if (endpoint.GetParameterThatIsResumableUpload(httpContext.RequestServices, BIND_ANY_TYPE) is not ResumableUploadParameterInfo parameterInfo)
            {
                await _next.Invoke(httpContext);
                return;
            }

            var store = httpContext.RequestServices.GetRequiredService<ITusStore>();
            var config = new DefaultTusConfiguration
            {
                Store = store,
                Events = new()
                {
                    //OnBeforeCreateAsync = (beforeCreateContext) => ValidateMetadata(beforeCreateContext, parameterInfo.TypeOfResumableUploadParam),
                    OnCreateCompleteAsync = SetUploadUrl
                }
            };

            var contextAdapter = CreateContextAdapter(httpContext, config);

            var handled = await TusV1EventRunner.Invoke(contextAdapter);

            // TODO: Can this happen?
            if (handled == ResultType.ContinueExecution)
            {
                httpContext.Response.StatusCode = 400;
                return;
            }

            if (await FileIsComplete(contextAdapter.FileId, store))
            {
                var file = await contextAdapter.StoreAdapter.GetFileAsync(contextAdapter.FileId, CancellationToken.None);
                httpContext.Features.Set(new ResumableUploadCompleteFeature(file));
                httpContext.Response.OnStarting(SetTusHeaders, contextAdapter);

                if (BIND_ANY_TYPE)
                {
                    var meta = await file.GetMetadataAsync(default);

                    // We need to replace the content type for the native model binders to be able to bind.
                    var contentType = resolveContentType(meta);

                    if (string.IsNullOrWhiteSpace(contentType) is false)
                        httpContext.Request.Headers.ContentType = new(contentType);
                    httpContext.Request.Body = await file.GetContentAsync(httpContext.RequestAborted);
                }

                await _next.Invoke(httpContext);

                return;
            }

            await httpContext.RespondToClient(contextAdapter.Response);
        }

        private Task SetTusHeaders(object state)
        {
            var contextAdapter = (ContextAdapter)state;
            var httpContext = contextAdapter.HttpContext;
            var isSuccessfulResponse = httpContext.Response.StatusCode is >= 200 and <= 299;

            if (isSuccessfulResponse)
            {
                try
                {
                    httpContext.RespondToClientWithHeadersOnly(contextAdapter.Response);
                }
                catch
                {
                    // Left blank
                }
            }

            return Task.CompletedTask;
        }

        private static ContextAdapter CreateContextAdapter(HttpContext httpContext, DefaultTusConfiguration config)
        {
            // No file id: https://localhost:5009/filesmodelbindingmvc
            // File id: https://localhost:5009/filesmodelbindingmvc?tdn-upload-id=asdf

            var request = DotnetCoreAdapterFactory.CreateRequestAdapter(httpContext, null);
            request.RequestUri = GetRequestUri(httpContext);

            return new ContextAdapter("/", MiddlewareUrlHelper.Instance)
            {
                Request = request,
                Configuration = config,
                CancellationToken = httpContext.RequestAborted,
                HttpContext = httpContext
            };

            static Uri GetRequestUri(HttpContext httpContext)
            {
                if (httpContext.Request.Query.TryGetValue(TUSDOTNET_UPLOAD_ID_QUERY_NAME, out var strings))
                    return new Uri("https://localhost/" + strings.First());

                return new Uri("https://localhost/");
            }
        }

        private Task SetUploadUrl(CreateCompleteContext context)
        {
            var url = new UriBuilder(context.HttpContext.Request.GetDisplayUrl());
            var query = HttpUtility.ParseQueryString(url.Query);
            query[TUSDOTNET_UPLOAD_ID_QUERY_NAME] = context.FileId;
            url.Query = query.ToString();

            context.SetUploadUrl(url.Uri);

            return Task.CompletedTask;
        }

        private static async Task ValidateMetadata(BeforeCreateContext beforeCreateContext, Type typeOfResumableUploadParameter)
        {
            var generic = typeof(MetadataValidator<>).MakeGenericType(typeOfResumableUploadParameter);
            var metadataValidator = (MetadataValidator)beforeCreateContext.HttpContext.RequestServices.GetService(generic);

            if (metadataValidator is null)
                return;

            var errors = await metadataValidator.ValidateMetadata(beforeCreateContext.Metadata);

            foreach (var item in errors)
            {
                beforeCreateContext.FailRequest(item);
            }
        }

        private static async Task<bool> FileIsComplete(string fileId, ITusStore store)
        {
            var length = store.GetUploadLengthAsync(fileId, CancellationToken.None);
            var offset = store.GetUploadOffsetAsync(fileId, CancellationToken.None);

            await Task.WhenAll(length, offset);

            return length.Result == offset.Result;
        }
    }
}
#endif