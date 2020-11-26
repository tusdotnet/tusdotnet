using System;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
    internal sealed class InternalFileId
    {
        public string FileId { get; set; }

        public InternalFileId(string fileId)
        {
            FileId = fileId;
        }

        public static async Task<InternalFileId> Create(ITusFileIdProvider provider)
        {
            var fileId = await provider.CreateId();
            return new InternalFileId(fileId);
        }

        public static async Task<InternalFileId> Create(ITusFileIdProvider provider, string fileId)
        {
            if (!await provider.ValidateId(fileId))
            {
                throw new TusStoreException("Invalid file id");
            }

            return new InternalFileId(fileId);
        }

        public override string ToString()
        {
            return FileId;
        }
    }
}
