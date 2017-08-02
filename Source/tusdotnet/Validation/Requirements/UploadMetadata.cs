using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;

namespace tusdotnet.Validation.Requirements
{
    internal class UploadMetadata : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            var request = context.Request;
            if (!request.Headers.ContainsKey(HeaderConstants.UploadMetadata))
            {
                return Done;
            }

            var validateMetadataResult = Metadata.ValidateMetadataHeader(request.Headers[HeaderConstants.UploadMetadata].First());
            if (!string.IsNullOrEmpty(validateMetadataResult))
            {
                BadRequest(validateMetadataResult);
            }

            return Done;
        }
    }
}
