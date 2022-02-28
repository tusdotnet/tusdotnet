using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class TusHandler : TusBaseHandlerEntryPoints
    {
        public override async Task<UploadRetrievingProcedureResponse> OnRetrieveOffset()
        {
            var offsetTask = Storage.GetOffset(Headers.UploadToken);
            var isCompleteTask = Storage.IsComplete(Headers.UploadToken);

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
            await Storage.Delete(Headers.UploadToken);

            return new()
            {
                Status = HttpStatusCode.NoContent
            };
        }

        public override async Task<CreateFileProcedureResponse> OnCreateFile(CreateFileContext createFileContext)
        {
            await Storage.CreateFile(Headers.UploadToken, createFileContext);

            return new() { Status = HttpStatusCode.Created };
        }

        public override async Task<UploadTransferProcedureResponse> OnWriteData(WriteDataContext writeDataContext)
        {
            try
            {
                await Storage.WriteData(Headers.UploadToken, writeDataContext);
            }
            catch (OperationCanceledException)
            {
                // Left blank. This is the case when the store does throws on cancellation instead of returning.
            }

            var uploadOffset = await Storage.GetOffset(Headers.UploadToken);

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
                    UploadIncomplete = true,
                    UploadOffset = uploadOffset
                };
            }

            await Storage.MarkComplete(Headers.UploadToken);

            return new()
            {
                UploadOffset = uploadOffset,
                UploadIncomplete = false,
            };
        }

        public override Task OnFileComplete()
        {
            return Task.CompletedTask;
        }
    }
}
