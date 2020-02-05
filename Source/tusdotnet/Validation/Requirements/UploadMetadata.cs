using System;
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
        private readonly Action<Dictionary<string, Metadata>> _cacheResult;

        public UploadMetadata(Action<Dictionary<string, Metadata>> cacheResult)
        {
            _cacheResult = cacheResult;
        }

        public override Task Validate(ContextAdapter context)
        {
            if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadMetadata))
            {
                _cacheResult?.Invoke(new Dictionary<string, Metadata>());
                return TaskHelper.Completed;
            }

            var metadataParserResult = MetadataParser.ParseAndValidate(
                context.Configuration.MetadataParsingStrategy,
                context.Request.GetHeader(HeaderConstants.UploadMetadata));

            if (metadataParserResult.Success)
            {
                _cacheResult?.Invoke(metadataParserResult.Metadata);
                return TaskHelper.Completed;
            }

            return BadRequest(metadataParserResult.ErrorMessage);
        }
    }
}
