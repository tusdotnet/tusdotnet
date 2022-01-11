using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal static class Tus2Validator
    {
        internal static async Task<bool> AssertFileExist(ITus2Storage store, string uploadToken, bool additionalCondition = true)
        {
            var fileExist = await store.FileExist(uploadToken);
            if (!fileExist && additionalCondition)
            {
                throw new Tus2AssertRequestException(HttpStatusCode.NotFound);
            }

            return fileExist;
        }

        internal static void AssertNoInvalidHeaders(Tus2Headers headers)
        {
            if (headers.UploadIncomplete.HasValue)
            {
                throw new Tus2AssertRequestException(HttpStatusCode.BadRequest, "Upload-Incomplete header is not allowed for procedure");
            }

            if (headers.UploadOffset.HasValue)
            {
                throw new Tus2AssertRequestException(HttpStatusCode.BadRequest, "Upload-Offset header is not allowed for procedure");
            }
        }

        internal static async Task AssertValidOffset(ITus2Storage store, string uploadToken, long? uploadOffset)
        {
            var existingOffset = await store.GetOffset(uploadToken);
            if (existingOffset != uploadOffset)
            {
                // TODO: In this case should we also return the offset in the response headers to prevent a round trip with retrieving the new offset?
                throw new Tus2AssertRequestException(HttpStatusCode.BadRequest, $"Invalid offset {uploadOffset}. File offset is at {existingOffset}");
            }
        }
    }
}
