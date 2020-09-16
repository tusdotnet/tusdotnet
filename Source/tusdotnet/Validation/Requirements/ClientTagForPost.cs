using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Interfaces;

namespace tusdotnet.Validation.Requirements
{
    internal class ClientTagForPost : Requirement
    {
        private readonly ITusClientTagStore _clientTagStore;

        public ClientTagForPost(ITusClientTagStore uploadTagStore)
        {
            _clientTagStore = uploadTagStore;
        }

        public override async Task Validate(ContextAdapter context)
        {
            if (_clientTagStore != null)
            {
                var uploadTag = context.Request.GetHeader(HeaderConstants.UploadTag);
                if (!string.IsNullOrWhiteSpace(uploadTag))
                {
                    var tagExists = !string.IsNullOrWhiteSpace((await _clientTagStore.ResolveUploadTagToFileIdAsync(uploadTag))?.FileId);

                    if (tagExists)
                    {
                        await Conflict("Upload-Tag is already in use");
                    }
                }
            }
        }
    }
}
