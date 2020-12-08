using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Stores
{
    internal sealed class InternalFileId
    {
        private readonly string _fileId;

        private InternalFileId(string fileId)
        {
            _fileId = fileId;
        }

        public static async Task<InternalFileId> CreateNew(ITusFileIdProvider provider, string metadata)
        {
            var fileId = await provider.CreateId(metadata);
            return new InternalFileId(fileId);
        }

        public static async Task<InternalFileId> Parse(ITusFileIdProvider provider, string fileId)
        {
            if (!await provider.ValidateId(fileId))
            {
                throw new TusStoreException("Invalid file id");
            }

            return new InternalFileId(fileId);
        }

        public static implicit operator string(InternalFileId fileId) => fileId._fileId;

        public override string ToString()
        {
            return _fileId;
        }
    }
}
