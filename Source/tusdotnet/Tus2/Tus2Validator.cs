using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal static class Tus2Validator
    {
        internal static async Task<bool> AssertFileExist(Tus2Storage store, string uploadToken, bool additionalCondition = true)
        {
            var fileExist = await store.FileExist(uploadToken);
            if (!fileExist && additionalCondition)
            {
                throw new Tus2AssertRequestException(HttpStatusCode.NotFound);
            }

            return fileExist;
        }

        internal static async Task AssertFileNotCompleted(Tus2Storage storage, string resourceId)
        {
            var fileIsComplete = await storage.IsComplete(resourceId);
            if (fileIsComplete)
            {
                throw new Tus2UploadCompletedException();
            }
        }

        internal static void AssertNoInvalidHeaders(Tus2Headers headers)
        {
            if (headers.UploadComplete.HasValue)
            {
                throw new Tus2AssertRequestException(HttpStatusCode.BadRequest, "Upload-Complete header is not allowed for procedure");
            }

            if (headers.UploadOffset.HasValue)
            {
                throw new Tus2AssertRequestException(HttpStatusCode.BadRequest, "Upload-Offset header is not allowed for procedure");
            }
        }

        internal static Task AssertValidOffset(long uploadOffsetFromStorage, long? uploadOffsetFromClient)
        {
            if (uploadOffsetFromStorage != uploadOffsetFromClient)
            {
                throw new Tus2MismatchingUploadOffsetException(uploadOffsetFromStorage, uploadOffsetFromClient ?? 0);
            }

            return Task.CompletedTask;
        }

        internal static void AssertValidResourceLength(long resourceLength, long uploadOffset, long? contentLength)
        {
            if (contentLength is null)
                return;

            if (uploadOffset + contentLength > resourceLength)
            {
                throw new Tus2AssertRequestException(HttpStatusCode.BadRequest, "Upload-Offset + Content-Length is larger than the resources allowed length from previous Content-Length");
            }
        }
    }
}
