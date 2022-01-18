using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class TusHandler : TusBaseHandlerEntryPoints
    {
        public override async Task<UploadRetrievingProcedureResponse> OnRetrieveOffset()
        {
            var offsetTask = Store.GetOffset(Headers.UploadToken);
            var isCompleteTask = Store.IsComplete(Headers.UploadToken);

            await Task.WhenAll(offsetTask, isCompleteTask);

            var offset = offsetTask.Result;
            var isComplete = isCompleteTask.Result;

            return new()
            {
                UploadOffset = offset,
                UploadIncomplete = !isComplete,
            };
        }

       
        public override async Task<UploadCancellationProcedureResponse> OnDelete()
        {
            await Store.Delete(Headers.UploadToken);

            return new()
            {
                Status = HttpStatusCode.NoContent
            };
        }

        public override async Task<CreateFileProcedureResponse> OnCreateFile(CreateFileContext createFileContext)
        {
            await Store.CreateFile(Headers.UploadToken, createFileContext);

            return new() { Status = HttpStatusCode.Created };
        }

        public override async Task<UploadTransferProcedureResponse> OnWriteData(WriteDataContext writeDataContext)
        {
            try
            {
                await Store.WriteData(Headers.UploadToken, writeDataContext);
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

            if (Headers.UploadIncomplete == true)
            {
                return new()
                {
                    Status = HttpStatusCode.Created,
                    UploadIncomplete = true
                };
            }

            await Store.MarkComplete(Headers.UploadToken);

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
