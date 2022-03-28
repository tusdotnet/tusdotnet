using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class Tus2StorageFacade
    {
        public Tus2StorageFacade(Tus2Storage storage)
        {
            Storage = storage;
        }

        public Tus2Storage Storage { get; }

        public virtual async Task<UploadRetrievingProcedureResponse> RetrieveOffset(RetrieveOffsetContext context)
        {
            var offsetTask = Storage.GetOffset(context.Headers.UploadToken);
            var isCompleteTask = Storage.IsComplete(context.Headers.UploadToken);

            await Task.WhenAll(offsetTask, isCompleteTask);

            var offset = offsetTask.Result;
            var isComplete = isCompleteTask.Result;

            return new()
            {
                Status = HttpStatusCode.NoContent,
                UploadOffset = offset,
                UploadIncomplete = !isComplete,
            };
        }

        public virtual async Task<UploadCancellationProcedureResponse> Delete(DeleteContext context)
        {
            await Storage.Delete(context.Headers.UploadToken);

            return new();
        }

        public virtual async Task<CreateFileProcedureResponse> CreateFile(CreateFileContext context)
        {
            await Storage.CreateFile(context.Headers.UploadToken, context);

            return new();
        }

        public async Task<UploadTransferProcedureResponse> WriteData(WriteDataContext context)
        {
            try
            {
                await Storage.WriteData(context.Headers.UploadToken, context);
            }
            catch (OperationCanceledException)
            {
                // Left blank. This is the case when the store does throws on cancellation instead of returning.
            }

            var uploadOffset = await Storage.GetOffset(context.Headers.UploadToken);

            if (context.CancellationToken.IsCancellationRequested)
            {
                return new()
                {
                    DisconnectClient = true
                };
            }

            if (context.Headers.UploadIncomplete == true)
            {
                return new()
                {
                    Status = HttpStatusCode.Created,
                    UploadIncomplete = true,
                    UploadOffset = uploadOffset
                };
            }

            await Storage.MarkComplete(context.Headers.UploadToken);

            return new()
            {
                Status = HttpStatusCode.Created,
                UploadOffset = uploadOffset,
                UploadIncomplete = false,
            };
        }

    }
}
