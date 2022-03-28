using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
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

        public override async Task<CreateFileProcedureResponse> CreateFile(CreateFileContext context)
        {
            _logger.LogInformation("Creating file {UploadToken}", context.Headers.UploadToken);

            var response = await _storage.CreateFile(context);

            _logger.LogInformation("File created? {Success}", response.Status == System.Net.HttpStatusCode.Created);

            return response;
        }

        public override async Task<UploadTransferProcedureResponse> WriteData(WriteDataContext context)
        {
            _logger.LogInformation("Receiving upload, starting at {UploadOffset}", context.Headers.UploadOffset);

            var response = await base.WriteData(context);

            _logger.LogInformation("Was success? {Success}", response.Status == System.Net.HttpStatusCode.Created);

            return response;
        }

        public override async Task<UploadRetrievingProcedureResponse> RetrieveOffset(RetrieveOffsetContext context)
        {
            _logger.LogInformation("Retrieving offset for {UploadToken}", context.Headers.UploadToken);

            var response = await base.RetrieveOffset(context);

            _logger.LogInformation("Offset is {UploadOffset}", response.UploadOffset);

            return response;
        }

        public override async Task<UploadCancellationProcedureResponse> Delete(DeleteContext context)
        {
            _logger.LogInformation("Deleting file {UploadToken}", context.Headers.UploadToken);

            var response = await base.Delete(context);

            _logger.LogInformation("File deleted? {Deleted}", response.Status == System.Net.HttpStatusCode.NoContent);

            return response;
        }

        public override Task FileComplete(FileCompleteContext context)
        {
            _logger.LogInformation("File {UploadToken} is complete", context.Headers.UploadToken);

            return base.FileComplete(context);
        }
    }
}
