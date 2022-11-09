using System;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Helpers;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class ContentType : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            var contentType = context.Request.Headers.ContentType;

            if (contentType?.Equals("application/offset+octet-stream", StringComparison.OrdinalIgnoreCase) != true)
            {
                var errorMessage = $"Content-Type {contentType} is invalid. Must be application/offset+octet-stream";
                return UnsupportedMediaType(errorMessage);
            }

            return TaskHelper.Completed;
        }
    }
}
