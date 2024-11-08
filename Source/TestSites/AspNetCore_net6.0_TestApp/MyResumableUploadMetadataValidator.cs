using tusdotnet.ModelBinding.Validation;
using tusdotnet.Models;

namespace AspNetCore_net6._0_TestApp
{
    public class MyResumableUploadMetadataValidator : MetadataValidator<MyMappedResumableUpload>
    {
        public override Task<IEnumerable<string>> ValidateMetadata(
            Dictionary<string, Metadata> metadata
        )
        {
            var result = new LinkedList<string>();

            if (!metadata.ContainsKey("name") || metadata["name"].HasEmptyValue)
            {
                result.AddLast("name metadata must be specified. ");
            }

            if (!metadata.ContainsKey("contentType") || metadata["contentType"].HasEmptyValue)
            {
                result.AddLast("contentType metadata must be specified. ");
            }

            return Task.FromResult<IEnumerable<string>>(result);
        }
    }
}
