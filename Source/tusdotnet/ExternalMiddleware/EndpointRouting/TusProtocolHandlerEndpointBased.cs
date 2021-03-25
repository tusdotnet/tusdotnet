#if endpointrouting

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.ExternalMiddleware.Core;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Parsers;

namespace tusdotnet.ExternalMiddleware.EndpointRouting
{
    internal class TusProtocolHandlerEndpointBased<TController, TConfigurator>
        where TController : TusController<TConfigurator>
        where TConfigurator : ITusConfigurator
    {
        internal async Task Invoke(HttpContext context)
        {
            var configurator = (ITusConfigurator)context.RequestServices.GetRequiredService<TConfigurator>();
            var options = await configurator.Configure(context);

            var controller = (TusController<TConfigurator>)context.RequestServices.GetRequiredService<TController>();

            var contextAdapter = CreateFakeContextAdapter(context, options);
            var responseStream = new MemoryStream();
            var responseHeaders = new Dictionary<string, string>();
            HttpStatusCode? responseStatus = null;
            contextAdapter.Response = new ResponseAdapter
            {
                Body = responseStream,
                SetHeader = (key, value) => responseHeaders[key] = value,
                SetStatus = status => responseStatus = status
            };

            var intentHandler = IntentAnalyzer.DetermineIntent(contextAdapter);

            if (intentHandler == IntentHandler.NotApplicable)
            {
                // Cannot determine intent so return not found.
                context.Response.StatusCode = 404;
                return;
            }

            var valid = await intentHandler.Validate();

            if (!valid)
            {
                // TODO: Optimize as there is not much worth in writing to a stream and then piping it to the response.
                context.Response.StatusCode = (int)responseStatus.Value;
                responseStream.Seek(0, SeekOrigin.Begin);
                await context.Response.BodyWriter.WriteAsync(responseStream.GetBuffer(), context.RequestAborted);

                return;
            }

            IActionResult result = null;
            IDictionary<string, string> headers = null;

            switch (intentHandler)
            {
                case CreateFileHandler c:
                    (result, headers) = await HandleCreate(context, controller);
                    break;
                case WriteFileHandler w:
                    (result, headers) = await HandleWriteFile(context, controller);
                    break;
                case GetFileInfoHandler f:
                    (result, headers) = await HandleGetFileInfo(context, await controller.Storage.GetStore());
                    break;
            }

            await context.Respond(result, headers);
        }

        private async Task<(IActionResult result, IDictionary<string, string> headers)> HandleGetFileInfo(HttpContext context, StoreAdapter store)
        {
            var fileId = (string)context.GetRouteValue("TusFileId");

            var result = new Dictionary<string, string>
            {
                {HeaderConstants.TusResumable, HeaderConstants.TusResumableValue },
                {HeaderConstants.CacheControl, HeaderConstants.NoStore }
            };

            var uploadMetadata = store.Extensions.Creation ? await store.GetUploadMetadataAsync(fileId, context.RequestAborted) : null;
            if (!string.IsNullOrEmpty(uploadMetadata))
            {
                result.Add(HeaderConstants.UploadMetadata, uploadMetadata);
            }

            var uploadLength = await store.GetUploadLengthAsync(fileId, context.RequestAborted);

            if (uploadLength != null)
            {
                result.Add(HeaderConstants.UploadLength, uploadLength.Value.ToString());
            }
            //else if (context.Configuration.Store is ITusCreationDeferLengthStore)
            //{
            //    context.Response.SetHeader(HeaderConstants.UploadDeferLength, "1");
            //}

            var uploadOffset = await store.GetUploadOffsetAsync(fileId, context.RequestAborted);

            //FileConcat uploadConcat = null;
            var addUploadOffset = true;
            //if (Context.Configuration.Store is ITusConcatenationStore tusConcatStore)
            //{
            //    uploadConcat = await tusConcatStore.GetUploadConcatAsync(Request.FileId, CancellationToken);

            //    // Only add Upload-Offset to final files if they are complete.
            //    if (uploadConcat is FileConcatFinal && uploadLength != uploadOffset)
            //    {
            //        addUploadOffset = false;
            //    }
            //}

            if (addUploadOffset)
            {
                result.Add(HeaderConstants.UploadOffset, uploadOffset.ToString());
            }

            //if (uploadConcat != null)
            //{
            //    (uploadConcat as FileConcatFinal)?.AddUrlPathToFiles(Context.Configuration.UrlPath);
            //    Response.SetHeader(HeaderConstants.UploadConcat, uploadConcat.GetHeader());
            //}

            return (new NoContentResult(), result);
        }

        private async Task<(IActionResult content, IDictionary<string, string> headers)> HandleWriteFile(HttpContext context, TusController<TConfigurator> controller)
        {
            //private Task WriteUploadLengthIfDefered()
            //{
            //    var uploadLenghtHeader = Request.GetHeader(HeaderConstants.UploadLength);
            //    if (uploadLenghtHeader != null && Store is ITusCreationDeferLengthStore creationDeferLengthStore)
            //    {
            //        return creationDeferLengthStore.SetUploadLengthAsync(Request.FileId, long.Parse(uploadLenghtHeader), Context.CancellationToken);
            //    }

            //    return TaskHelper.Completed;
            //}

            var writeContext = new WriteContext
            {
                FileId = (string)context.GetRouteValue("TusFileId"),
                // Callback to later support trailing checksum headers
                GetChecksumProvidedByClient = () => GetChecksumFromContext(context),
                RequestStream = context.Request.Body,
                UploadOffset = long.Parse(context.Request.Headers["Upload-Offset"].First())
            };

            await controller.Write(writeContext, context.RequestAborted);

            if (writeContext.ClientDisconnectedDuringRead)
            {
                return (new OkResult(), null);
            }

            if (writeContext.IsComplete && !writeContext.IsPartialFile)
            {
                await controller.FileCompleted(new() { FileId = writeContext.FileId }, context.RequestAborted);
            }

            return (new NoContentResult(), GetCreateHeaders(writeContext.FileExpires, writeContext.UploadOffset));
        }

        private Checksum GetChecksumFromContext(HttpContext context)
        {
            var header = context.Request.Headers["Upload-Checksum"].FirstOrDefault();

            return header != null ? new Checksum(header) : null;
        }

        private async Task<(IActionResult content, IDictionary<string, string> headers)> HandleCreate(HttpContext context, TusController<TConfigurator> controller)
        {
            if (!await controller.AuthorizeForAction(context, nameof(controller.Create)))
                return (new ForbidResult(), null);

            // TODO: Replace with typed headers
            var metadata = context.Request.Headers["Upload-Metadata"].FirstOrDefault();
            var uploadLength = context.Request.Headers["Upload-Length"].FirstOrDefault();

            var createContext = new CreateContext
            {
                UploadMetadata = metadata,
                Metadata = MetadataParser.ParseAndValidate(MetadataParsingStrategy.AllowEmptyValues, metadata).Metadata,
                UploadLength = long.Parse(uploadLength),
            };

            var result = await controller.Create(createContext, context.RequestAborted);

            if (result is not OkResult)
                return (result, null);

            var isEmptyFile = createContext.UploadLength == 0;

            if (isEmptyFile)
            {
                result = await controller.FileCompleted(new() { FileId = createContext.FileId }, context.RequestAborted);

                if (result is not OkResult)
                    return (result, null);
            }

            var createResult = new CreatedResult($"{context.Request.GetDisplayUrl().TrimEnd('/')}/{createContext.FileId}", null);

            return (createResult, GetCreateHeaders(createContext.FileExpires, createContext.UploadOffset));
        }

        private Dictionary<string, string> GetCreateHeaders(DateTimeOffset? expires, long? uploadOffset)
        {
            var result = new Dictionary<string, string>();
            if (expires != null)
            {
                result.Add(HeaderConstants.UploadExpires, expires.Value.ToString("R"));
            }

            if (uploadOffset != null)
            {
                result.Add(HeaderConstants.UploadOffset, uploadOffset.Value.ToString());
            }

            result.Add(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);

            return result;
        }

        private ContextAdapter CreateFakeContextAdapter(HttpContext context, EndpointOptions options)
        {
            var urlPath = (string)context.GetRouteValue("TusFileId");

            if (string.IsNullOrWhiteSpace(urlPath))
            {
                urlPath = context.Request.Path;
            }
            else
            {
                var span = context.Request.Path.ToString().TrimEnd('/').AsSpan();
                urlPath = span.Slice(0, span.LastIndexOf('/')).ToString();
            }

            var config = new DefaultTusConfiguration
            {
                Expiration = options.Expiration,
                Store = options.Store,
                UrlPath = urlPath
            };

            var adapter = ContextAdapterBuilder.FromHttpContext(context, config);
            adapter.EndpointOptions = options;

            return adapter;
        }
    }
}

#endif