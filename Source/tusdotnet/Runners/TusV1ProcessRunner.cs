#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Constants;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;

namespace tusdotnet.Runners.TusV1Process
{
    public class TusV1ProcessRunner
    {
        // TODO: Check if we need to add an abstraction over checksum trailers or if it's good enough to hack it with the http context accessor.
        // TODO: Do not use this config but rather individual parts that we need (store, lock provider etc)
        private readonly Func<HttpContext, Task<DefaultTusConfiguration>> _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TusV1ProcessRunner(Func<HttpContext, Task<DefaultTusConfiguration>> config, IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public virtual async Task<CreateFileResponse> CreateFile(CreateFileRequest request)
        {
            var config = await GetConfig();
            var context = request.ToContextAdapter(config);

            if (request.IsPartialFile)
            {
                context.Request.Headers[HeaderConstants.UploadConcat] = "partial";
                await new ConcatenateFilesHandler(context).Invoke();
            }
            else
            {
                await new CreateFileHandler(context).Invoke();
            }

            return CreateFileResponse.FromContextAdapter(context);
        }

        public virtual async Task<WriteFileResponse> WriteFile(WriteFileRequest request)
        {
            var config = await GetConfig();
            var context = request.ToContextAdapter(config);

            await new WriteFileHandler(context, request.InitiatedFromCreationWithUpload).Invoke();

            var response = WriteFileResponse.FromContextAdapter(context);

            // Load current offset from store if we did not get one from the handler, e.g. if the client discconected.
            if (response.UploadOffset == -1)
                response.UploadOffset = await config.Store.GetUploadOffsetAsync(request.FileId, CancellationToken.None);

            return response;
        }

        public virtual async Task<FileInfoResponse> GetFileInfo(FileInfoRequest request)
        {
            var config = await GetConfig();
            var context = request.ToContextAdapter(config);

            await new GetFileInfoHandler(context).Invoke();

            var response = FileInfoResponse.FromContextAdapter(context);

            return response;
        }


        private async Task<DefaultTusConfiguration> GetConfig()
        {
            var conf = await _config(_httpContextAccessor.HttpContext);
            return conf;
        }

        internal async Task<DeleteFileResponse> DeleteFile(DeleteFileRequest request)
        {
            var config = await GetConfig();
            var context = request.ToContextAdapter(config);

            await new DeleteFileHandler(context).Invoke();

            var response = DeleteFileResponse.FromContextAdapter(context);

            return response;
        }

        internal async Task<ServerOptionsResponse> GetServerOptions(ServerOptionsRequest request)
        {
            var config = await GetConfig();
            var context = request.ToContextAdapter(config);

            await new GetOptionsHandler(context).Invoke();

            var response = ServerOptionsResponse.FromContextAdapter(context);

            // Hack as we don't yet have a http context when running write file resulting in no trailing headers existing.
            response.Extensions = new List<string>(response.Extensions).SkipWhile(x => x == ExtensionConstants.ChecksumTrailer).ToList();

            return response;
        }

        internal async Task<ConcatenateFilesResponse> ConcatenateFiles(ConcatenateFilesRequest request)
        {
            var config = await GetConfig();

            var context = request.ToContextAdapter(config);

            await new ConcatenateFilesHandler(context).Invoke();

            return ConcatenateFilesResponse.FromContextAdapter(context);
        }
    }
}

#endif