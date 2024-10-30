using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using tusdotnet.Tus2;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class MyTusHandler : TusHandler
    {
        private readonly ILogger _logger;
        private readonly Tus2StorageFacade _storage;

        public MyTusHandler(ILoggerFactory loggerFactory, Tus2StorageFacade storage)
            : base(storage)
        {
            _logger = loggerFactory.CreateLogger(nameof(MyTusHandler));
            _storage = storage;
        }

        public override bool AllowClientToDeleteFile => true;

        public override bool EnableReportingOfProgress => false;

        public override TusHandlerLimits? Limits =>
            new()
            {
                Expiration = TimeSpan.FromHours(24),
                MaxSize = 1024 * 1024 * 1024,
                MaxAppendSize = 100 * 1024 * 1024,
                MinSize = 1024,
                MinAppendSize = 5 * 1024 * 1024
            };

        public override async Task<CreateFileProcedureResponse> CreateFile(
            CreateFileContext context
        )
        {
            _logger.LogInformation(
                "CreateFile: Creating file {UploadToken}",
                context.Headers.ResourceId
            );

            var response = await _storage.CreateFile(context);

            _logger.LogInformation(
                "CreateFile: File created? {Success}",
                response.Status == System.Net.HttpStatusCode.Created
            );

            return response;
        }

        public override async Task<UploadTransferProcedureResponse> WriteData(
            WriteDataContext context
        )
        {
            _logger.LogInformation(
                "WriteData: Receiving upload, starting at {UploadOffset}",
                context.Headers.UploadOffset
            );

            var response = await base.WriteData(context);

            _logger.LogInformation(
                "WriteData: Was success? {Success}",
                response.Status == System.Net.HttpStatusCode.Created
            );

            return response;
        }

        public override async Task<UploadRetrievingProcedureResponse> RetrieveOffset(
            RetrieveOffsetContext context
        )
        {
            _logger.LogInformation(
                "RetrieveOffset: Retrieving offset for {UploadToken}",
                context.Headers.ResourceId
            );

            var response = await base.RetrieveOffset(context);

            _logger.LogInformation(
                "RetrieveOffset: Offset is {UploadOffset}",
                response.UploadOffset
            );

            return response;
        }

        public override async Task<UploadCancellationProcedureResponse> Delete(
            DeleteContext context
        )
        {
            _logger.LogInformation(
                "Delete: Deleting file {UploadToken}",
                context.Headers.ResourceId
            );

            var response = await base.Delete(context);

            _logger.LogInformation(
                "Delete: File deleted? {Deleted}",
                response.Status == System.Net.HttpStatusCode.NoContent
            );

            return response;
        }

        public override Task FileComplete(FileCompleteContext context)
        {
            _logger.LogInformation(
                "FileComplete: File {UploadToken} is complete",
                context.Headers.ResourceId
            );

            return base.FileComplete(context);
        }

        public override async Task<Uri> GetContentLocation(string resourceId, bool uploadCompleted)
        {
            if (!uploadCompleted)
                return null;

            return new Uri("/files-tus-2-status/" + resourceId, UriKind.Relative);
        }
    }
}
