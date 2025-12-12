using System.IO;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Adapters
{
    internal sealed class WriteFileContextForCreationWithUpload
    {
        internal bool FileContentIsAvailable { get; }

        internal Stream Body { get; }

#if pipelines
        internal static async Task<WriteFileContextForCreationWithUpload> FromCreationContext(
            ContextAdapter creationContext
        )
        {
            if (!creationContext.StoreAdapter.Extensions.CreationWithUpload)
                return new WriteFileContextForCreationWithUpload(creationContext, false, null);

            if (creationContext.Request.Headers.UploadLength == 0)
                return new WriteFileContextForCreationWithUpload(creationContext, false, null);

            if (creationContext.Configuration.UsePipelinesIfAvailable)
            {
                // Check if there is any file content available in the request.
                bool hasData;
                try
                {
                    System.IO.Pipelines.ReadResult result =
                        await creationContext.Request.BodyReader.ReadAsync();
                    if (result.Buffer.IsEmpty)
                    {
                        hasData = false;
                    }
                    else
                    {
                        hasData = !result.IsCanceled && result.Buffer.Length > 0;
                        // Advance to "examined" which will cause the pipe reader to keep the data in its internal buffer.
                        creationContext.Request.BodyReader.AdvanceTo(
                            result.Buffer.Start,
                            result.Buffer.End
                        );
                    }
                }
                catch
                {
                    hasData = false;
                }

                // No need to add the extra byte as the pipe reader already contains the buffered input.
                return new WriteFileContextForCreationWithUpload(creationContext, hasData, null);
            }
            else
            {
                // Check if there is any file content available in the request.
                var buffer = new byte[1];
                var hasData =
                    await creationContext.Request.Body.ReadAsync(
                        buffer,
                        0,
                        1,
                        System.Threading.CancellationToken.None
                    ) > 0;

                return new WriteFileContextForCreationWithUpload(
                    creationContext,
                    hasData,
                    buffer[0]
                );
            }
        }
#else
        internal static async Task<WriteFileContextForCreationWithUpload> FromCreationContext(
            ContextAdapter creationContext
        )
        {
            if (!creationContext.StoreAdapter.Extensions.CreationWithUpload)
                return new WriteFileContextForCreationWithUpload(creationContext, false, null);

            if (creationContext.Request.Headers.UploadLength == 0)
                return new WriteFileContextForCreationWithUpload(creationContext, false, null);

            // Check if there is any file content available in the request.
            var buffer = new byte[1];
            var hasData = await creationContext.Request.Body.ReadAsync(buffer, 0, 1) > 0;

            return new WriteFileContextForCreationWithUpload(creationContext, hasData, buffer[0]);
        }
#endif

        private WriteFileContextForCreationWithUpload(
            ContextAdapter creationContext,
            bool hasData,
            byte? preReadByteFromStream
        )
        {
            FileContentIsAvailable = hasData;
            Body = preReadByteFromStream.HasValue
                ? new ReadOnlyStreamWithPreReadByte(
                    creationContext.Request.Body,
                    preReadByteFromStream.Value
                )
                : creationContext.Request.Body;
        }
    }
}
