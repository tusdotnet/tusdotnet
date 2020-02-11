using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace tusdotnet.Adapters
{
    internal sealed class WriteFileContextForCreationWithUpload
    {
        internal ContextAdapter Context { get; }

        internal Dictionary<string, string> ResponseHeaders { get; }

        internal Stream ResponseStream { get; }

        internal int? UploadOffset => GetUploadOffset();

        internal bool FileContentIsAvailable { get; }

        internal static async Task<WriteFileContextForCreationWithUpload> CreateFromContext(ContextAdapter creationContext, string fileId)
        {
            // Check if there is any file content available in the request.
            var buffer = new byte[1];
            var hasData = await creationContext.Request.Body.ReadAsync(buffer, 0, 1) > 0;

            return new WriteFileContextForCreationWithUpload(creationContext, fileId, hasData, buffer[0]);
        }

        internal async Task<int?> SaveFileContent()
        {
            var authResponse = await EventHelper.Validate<AuthorizeContext>(Context, ctx =>
            {
                ctx.Intent = IntentType.WriteFile;
                ctx.FileConcatenation = null;
            });

            if (authResponse == ResultType.StopExecution)
            {
                return 0;
            }

            var writeFileHandler = new WriteFileHandler(Context);

            if (!await writeFileHandler.Validate())
            {
                return 0;
            }

            await writeFileHandler.Invoke();

            return UploadOffset;
        }

        private WriteFileContextForCreationWithUpload(ContextAdapter creationContext, string fileId, bool hasData, byte preReadByte)
        {
            ResponseHeaders = new Dictionary<string, string>();
            ResponseStream = new MemoryStream();

            FileContentIsAvailable = hasData;

            Context = new ContextAdapter
            {
                CancellationToken = creationContext.CancellationToken,
                Configuration = creationContext.Configuration,
                HttpContext = creationContext.HttpContext,
                Request = CreateWriteFileRequest(creationContext, fileId, preReadByte),
                Response = CreateWriteFileResponse(),
#if netfull
                OwinContext = creationContext.OwinContext
#endif
            };
        }

        private ResponseAdapter CreateWriteFileResponse()
        {
            return new ResponseAdapter
            {
                Body = ResponseStream,
                SetHeader = (key, value) => ResponseHeaders[key] = value,
                SetStatus = _ => { }
            };
        }

        private RequestAdapter CreateWriteFileRequest(ContextAdapter context, string fileId, byte preReadByte)
        {
            var uriBuilder = new UriBuilder(context.Request.RequestUri);
            uriBuilder.Path = uriBuilder.Path + "/" + fileId;

            var writeFileRequest = new RequestAdapter(context.Configuration.UrlPath)
            {
                Body = new ReadOnlyStreamWithPreReadByte(context.Request.Body, preReadByte),
                Method = context.Request.Method,
                RequestUri = uriBuilder.Uri,
                Headers = context.Request.Headers
            };

            writeFileRequest.Headers[HeaderConstants.UploadOffset] = new List<string>(1) { "0" };
            writeFileRequest.Headers.Remove(HeaderConstants.UploadLength);

            return writeFileRequest;
        }

        private int? GetUploadOffset()
        {
            if (!ResponseHeaders.TryGetValue(HeaderConstants.UploadOffset, out var uploadOffset))
            {
                return null;
            }

            if (int.TryParse(uploadOffset, out var i))
            {
                return i;
            }

            return null;
        }
    }
}
