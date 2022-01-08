using System;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    public class TusBaseHandler : TusBaseHandlerEntryPoints
    {
        public override async Task<UploadRetrievingProcedureResponse> RetrieveOffset()
        {
            var offset = await TusContext.Store.GetOffset(TusContext.Headers.UploadToken);

            return new()
            {
                UploadOffset = offset
            };
        }

       
        public override async Task<UploadCancellationProcedureResponse> Delete()
        {
            await TusContext.Store.Delete(TusContext.Headers.UploadToken);

            return new()
            {
                Status = HttpStatusCode.NoContent
            };
        }

        public override async Task<CreateFileProcedureResponse> CreateFile(CreateFileContext createFileContext)
        {
            await TusContext.Store.CreateFile(TusContext.Headers.UploadToken, new() { Metadata = createFileContext.Metadata });

            return new() { Status = HttpStatusCode.Created };
        }

        public override async Task<UploadTransferProcedureResponse> WriteData(WriteDataContext writeDataContext)
        {
            try
            {
                var options = new WriteFileOptions()
                {
                    Metadata = writeDataContext.Metadata
                };

                await TusContext.Store.AppendData(TusContext.Headers.UploadToken, writeDataContext.BodyReader, writeDataContext.CancellationToken, options);
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
            await FileComplete();

            return new()
            {
                Status = HttpStatusCode.Created,
                UploadIncomplete = false
            };
        }

        // Possibly we don't need this as the tus2 handling is much simpler.
        public override Task FileComplete()
        {
            return Task.CompletedTask;
        }
    }
}
