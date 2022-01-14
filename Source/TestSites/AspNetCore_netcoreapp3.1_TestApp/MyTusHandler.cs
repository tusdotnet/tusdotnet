using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using tusdotnet.Tus2;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class MyTusHandler : TusHandler
    {
        private readonly ILogger _logger;

        public MyTusHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(nameof(MyTusHandler));
        }

        public override async Task<CreateFileProcedureResponse> OnCreateFile(CreateFileContext createFileContext)
        {
            _logger.LogInformation("Creating file {UploadToken}", Headers.UploadToken);

            var response = await base.OnCreateFile(createFileContext);

            _logger.LogInformation("File created? {Success}", response.Status == System.Net.HttpStatusCode.Created);

            return response;
        }

        public override async Task<UploadTransferProcedureResponse> OnWriteData(WriteDataContext writeDataContext)
        {
            _logger.LogInformation("Receiving upload, starting at {UploadOffset}", Headers.UploadOffset);
            
            var response = await base.OnWriteData(writeDataContext);

            _logger.LogInformation("Was success? {Success}", response.Status == System.Net.HttpStatusCode.Created);

            return response;
        }

        public override async Task<UploadRetrievingProcedureResponse> OnRetrieveOffset()
        {
            _logger.LogInformation("Retrieving offset for {UploadToken}", Headers.UploadToken);

            var response = await base.OnRetrieveOffset();

            _logger.LogInformation("Offset is {UploadOffset}", response.UploadOffset);

            return response;
        }

        public override async Task<UploadCancellationProcedureResponse> OnDelete()
        {
            _logger.LogInformation("Deleting file {UploadToken}", Headers.UploadToken);

            var response = await base.OnDelete();

            _logger.LogInformation("File deleted? {Deleted}", response.Status == System.Net.HttpStatusCode.NoContent);

            return response;
        }

        public override Task OnFileComplete()
        {
            _logger.LogInformation("File {UploadToken} is complete", Headers.UploadToken);

            return base.OnFileComplete();
        }
    }
}
