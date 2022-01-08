using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using tusdotnet.Tus2;

namespace AspNetCore_netcoreapp3._1_TestApp
{
    public class MyTusHandler : TusBaseHandler
    {
        private readonly ILogger _logger;

        public MyTusHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(nameof(MyTusHandler));
        }

        public override async Task<CreateFileProcedureResponse> CreateFile(CreateFileContext createFileContext)
        {
            _logger.LogInformation("Creating file {UploadToken}", TusContext.Headers.UploadToken);

            var response = await base.CreateFile(createFileContext);

            _logger.LogInformation("File created? {Success}", response.Status == System.Net.HttpStatusCode.Created);

            return response;
        }

        public override async Task<UploadTransferProcedureResponse> WriteData(WriteDataContext writeDataContext)
        {
            _logger.LogInformation("Receiving upload, starting at {UploadOffset}", TusContext.Headers.UploadOffset);

            var response = await base.WriteData(writeDataContext);

            _logger.LogInformation("Was success? {Success}", response.Status == System.Net.HttpStatusCode.Created);

            return response;
        }

        public override async Task<UploadRetrievingProcedureResponse> RetrieveOffset()
        {
            _logger.LogInformation("Retrieving offset for {UploadToken}", TusContext.Headers.UploadToken);

            var response = await base.RetrieveOffset();

            _logger.LogInformation("Offset is {UploadOffset}", response.UploadOffset);

            return response;
        }
    }
}
