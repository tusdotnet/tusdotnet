﻿#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using tusdotnet.Tus2.ResponseModels;

namespace tusdotnet.Tus2
{
    internal static class Tus2Endpoint
    {
        internal static async Task Invoke<T>(HttpContext httpContext, string? resourceId)
            where T : TusHandler
        {
            // TODO Remove this and just use the MapX methods on the endpoint builder one step up.
            if (httpContext.Request.Method.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                httpContext.Response.Headers.Add(
                    "Allow",
                    "PUT, PATCH, POST, HEAD, DELETE, OPTIONS"
                );
                await httpContext.Error(HttpStatusCode.MethodNotAllowed);
                return;
            }

            var headerParser = httpContext.RequestServices.GetRequiredService<IHeaderParser>();

            var headers = headerParser.Parse(httpContext);
            headers.ResourceId = resourceId;

            Tus2BaseResponse? response = null;

            try
            {
                var handler = httpContext.RequestServices.GetRequiredService<T>();

                if (
                    httpContext.Request.Method.Equals("options", StringComparison.OrdinalIgnoreCase)
                )
                {
                    response = new OptionsResponse(handler.Limits);
                }
                else
                {
                    response = await InvokeHandler(handler, httpContext, headers);
                }
            }
            catch (Tus2ProblemDetailsException problemDetailsException)
            {
                await WriteProblemDetails(httpContext, problemDetailsException);
                response = null;
            }
            finally
            {
                if (!httpContext.Response.HasStarted)
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
        }

        private static async Task WriteProblemDetails(
            HttpContext httpContext,
            Tus2ProblemDetailsException problemDetailsException
        )
        {
            var details = problemDetailsException.GetProblemDetails();
            httpContext.Response.StatusCode = (int)problemDetailsException.Status;
            await httpContext.Response.WriteAsJsonAsync(details);
        }

        private static async Task<Tus2BaseResponse> InvokeHandler(
            TusHandler handler,
            HttpContext httpContext,
            Tus2Headers headers
        )
        {
            var method = httpContext.Request.Method;

            var uploadManager = await GetOngoingUploadManager(httpContext);

            if (method.Equals("head", StringComparison.OrdinalIgnoreCase))
            {
                if (
                    AssertResourceId<UploadRetrievingProcedureResponse>(headers.ResourceId)
                    is var response
                        and not null
                )
                    return response;

                var retrieveOffsetContext = CreateContext<RetrieveOffsetContext>(
                    httpContext,
                    headers
                );
                return await Tus2HandlerInvoker.RetrieveOffsetEntryPoint(
                    handler,
                    retrieveOffsetContext,
                    uploadManager
                );
            }
            else if (method.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                if (
                    AssertResourceId<UploadCancellationProcedureResponse>(headers.ResourceId)
                    is var response
                        and not null
                )
                    return response;

                var deleteContext = CreateContext<DeleteContext>(httpContext, headers);
                return await Tus2HandlerInvoker.DeleteEntryPoint(
                    handler,
                    deleteContext,
                    uploadManager
                );
            }
            else if (method.Equals("patch", StringComparison.OrdinalIgnoreCase))
            {
                if (
                    AssertContentType<UploadTransferProcedureResponse>(headers.ContentType)
                    is var response
                        and not null
                )
                {
                    return response;
                }
            }

            var newlyCreatedFile = headers.ResourceId is null;

            headers.ResourceId ??= GenerateResourceId();

            var writeFileContext = CreateContext<WriteDataContext>(httpContext, headers);
            var writeResponse = await Tus2HandlerInvoker.WriteDataEntryPoint(
                handler,
                writeFileContext,
                uploadManager
            );

            if (writeResponse.UploadComplete)
            {
                // TODO: Might want to pass the uploadmanager combined CT to this context
                // instead of the one from the request.
                var fileCompleteContext = CreateContext<FileCompleteContext>(httpContext, headers);
                await handler.FileComplete(fileCompleteContext);
            }

            writeResponse.ResourceWasJustCreated = newlyCreatedFile;
            writeResponse.ResourceId = headers.ResourceId;

            return writeResponse;
        }

        private static Tus2BaseResponse? AssertContentType<T>(string? contentType)
            where T : Tus2BaseResponse, new()
        {
            if (contentType is not "application/partial-upload")
            {
                return new T()
                {
                    ErrorMessage = "Content-Type must be application/partial-upload",
                    Status = HttpStatusCode.UnsupportedMediaType
                };
            }

            return null;
        }

        private static string GenerateResourceId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static Tus2BaseResponse? AssertResourceId<T>(string? resourceId)
            where T : Tus2BaseResponse, new()
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return new T()
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessage = "Missing Upload-Token header"
                };
            }

            return null;
        }

        private static T CreateContext<T>(HttpContext httpContext, Tus2Headers headers)
            where T : Tus2Context, new()
        {
            return new T
            {
                HttpContext = httpContext,
                CancellationToken = httpContext.RequestAborted,
                Headers = headers
            };
        }

        private static Task<IOngoingUploadManager> GetOngoingUploadManager(HttpContext context)
        {
            var factory = context.RequestServices.GetService<IOngoingUploadManagerFactory>();

            if (factory != null)
                return factory.CreateOngoingUploadManager();

            return Task.FromResult(
                context.RequestServices.GetRequiredService<IOngoingUploadManager>()
            );
        }
    }
}
