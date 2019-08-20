using System;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
    public sealed class InternalFileId: ITusFileIdProvider
    {
        public string FileId { get; private set; }

        public InternalFileId()
        {
            FileId = Guid.NewGuid().ToString("n");
        }

        public ITusFileIdProvider Use(string fileId)
        {
            if (!Guid.TryParseExact(fileId, "n", out var _))
            {
                throw new TusStoreException("Invalid file id");
            }

            FileId = fileId;

            return this;
        }

        public override string ToString()
        {
            return FileId;
        }
    }
}
