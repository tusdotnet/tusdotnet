#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.IntentHandlers;
using tusdotnet.Models;

namespace tusdotnet.Runners
{
    public class TusV1Process
    {
        // TODO: Do not use this config but rather individual parts that we need (store, lock provider etc)
        private readonly Func<HttpContext, Task<DefaultTusConfiguration>> _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TusV1Process(Func<HttpContext, Task<DefaultTusConfiguration>> config, IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public virtual async Task<CreateFileResponse> CreateFile(CreateFileRequest request)
        {
            var config = await GetConfig();
            var context = request.ToContextAdapter(config);

            await new CreateFileHandler(context).Invoke();

            return CreateFileResponse.FromContextAdapter(context);
        }

        public virtual async Task<WriteFileResponse> WriteFile(WriteFileRequest request)
        {
            var config = await GetConfig();
            var context = request.ToContextAdapter(config);

            // TODO: how to determine if initiated from creation with upload
            await new WriteFileHandler(context, false).Invoke();

            var response = WriteFileResponse.FromContextAdapter(context);

            if (response.UploadOffset == -1)
            {
                response.UploadOffset = await config.Store.GetUploadOffsetAsync(request.FileId, CancellationToken.None);
            }

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
            conf.AllowedExtensions = TusExtensions.All.Except(TusExtensions.ChecksumTrailer);
            return conf;
        }
    }
}

#endif