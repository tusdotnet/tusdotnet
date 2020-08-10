using System;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
    internal sealed class InternalFileId
    {
        public string FileId { get; set; }

        public InternalFileId(ITusFileIdProvider provider)
        {
            FileId = provider.CreateId();
        }

        public InternalFileId(ITusFileIdProvider provider, string fileId)
        {
            if (!provider.ValidateId(fileId))
            {
                throw new TusStoreException("Invalid file id");
            }

            FileId = fileId;
        }

        public override string ToString()
        {
            return FileId;
        }
    }
}
