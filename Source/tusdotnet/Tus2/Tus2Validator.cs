using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace tusdotnet.Tus2
{
    internal static class Tus2Validator
    {
        private static readonly HashSet<char> _invalidFileNameChars = new(Path.GetInvalidFileNameChars());

        internal static async Task<bool> AssertFileExist(Tus2DiskStore store, string uploadToken, bool additionalCondition = true)
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

        internal static async Task AssertValidOffset(Tus2DiskStore store, string uploadToken, long? uploadOffset)
        {
            var existingOffset = await store.GetOffset(uploadToken);
            if (existingOffset != uploadOffset)
            {
                // TODO: In this case should we also return the offset in the response headers to prevent a round trip with retrieving the new offset?
                throw new Tus2AssertRequestException(HttpStatusCode.BadRequest, $"Invalid offset {uploadOffset}. File offset is at {existingOffset}");
            }
        }

        internal static string CleanUploadToken(string uploadToken)
        {
            var result = uploadToken;
            // TODO: This should not be here but in the disk store

            var span = result.ToCharArray();

            for (int i = 0; i < span.Length; i++)
            {
                if (_invalidFileNameChars.Contains(span[i]))
                    span[i] = '_';
            }

            return new string(span);
        }
    }
}
