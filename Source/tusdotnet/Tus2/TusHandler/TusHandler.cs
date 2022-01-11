using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class TusHandler : TusBaseHandlerEntryPoints
    {
        public override async Task<UploadRetrievingProcedureResponse> OnRetrieveOffset()
        {
            var offset = await TusContext.Store.GetOffset(TusContext.Headers.UploadToken);

            return new()
            {
                UploadOffset = offset
            };
        }

       
        public override async Task<UploadCancellationProcedureResponse> OnDelete()
        {
            await TusContext.Store.Delete(TusContext.Headers.UploadToken);

            return new()
            {
                Status = HttpStatusCode.NoContent
            };
        }

        public override async Task<CreateFileProcedureResponse> OnCreateFile(CreateFileContext createFileContext)
        {
            await TusContext.Store.CreateFile(TusContext.Headers.UploadToken, createFileContext);

            return new() { Status = HttpStatusCode.Created };
        }

        public override async Task<UploadTransferProcedureResponse> OnWriteData(WriteDataContext writeDataContext)
        {
            try
            {
                await TusContext.Store.WriteData(TusContext.Headers.UploadToken, writeDataContext);
            }
            catch (OperationCanceledException)
            {
                // Left blank. This is the case when the store does throws on cancellation instead of returning.
            }

            if (writeDataContext.CancellationToken.IsCancellationRequested)
            {
                return new()
                {
                    DisconnectClient = true
                };
            }

            if (TusContext.Headers.UploadIncomplete == true)
            {
                return new()
                {
                    Status = HttpStatusCode.Created,
                    UploadIncomplete = true
                };
            }

            await TusContext.Store.MarkComplete(TusContext.Headers.UploadToken);

            return new()
            {
                Status = HttpStatusCode.Created,
                UploadIncomplete = false,
                UploadCompleted = true
            };
        }

        public override Task OnFileComplete()
        {
            return Task.CompletedTask;
        }
    }
}
