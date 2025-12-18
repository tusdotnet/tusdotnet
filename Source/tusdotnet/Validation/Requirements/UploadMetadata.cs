using System.Collections.Generic;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;
using tusdotnet.Models;
using tusdotnet.Parsers;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadMetadata : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadMetadata))
            {
                context.ParsedRequest.Metadata = [];
                return TaskHelper.Completed;
            }

            var metadataParserResult = MetadataParser.ParseAndValidate(
                context.Configuration.MetadataParsingStrategy,
                context.Request.Headers.UploadMetadata
            );

            if (metadataParserResult.Success)
            {
                context.ParsedRequest.Metadata = metadataParserResult.Metadata;
                return TaskHelper.Completed;
            }

            return BadRequest(metadataParserResult.ErrorMessage);
        }
    }
}
