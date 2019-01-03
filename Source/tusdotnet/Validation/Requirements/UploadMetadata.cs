using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Models;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadMetadata : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            var request = context.Request;
            if (!request.Headers.ContainsKey(HeaderConstants.UploadMetadata))
            {
                return TaskHelper.Completed;
            }

            var validateMetadataResult = Metadata.ValidateMetadataHeader(request.Headers[HeaderConstants.UploadMetadata][0]);
            if (!string.IsNullOrEmpty(validateMetadataResult))
            {
                return BadRequest(validateMetadataResult);
            }

            return TaskHelper.Completed;
        }
    }
}
