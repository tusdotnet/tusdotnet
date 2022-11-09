using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Helpers;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadOffset : Requirement
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadOffset))
            {
                return BadRequest($"Missing {HeaderConstants.UploadOffset} header");
            }

            if (!long.TryParse(context.Request.Headers[HeaderConstants.UploadOffset], out long requestOffset))
            {
                return BadRequest($"Could not parse {HeaderConstants.UploadOffset} header");
            }

            if (requestOffset < 0)
            {
                return BadRequest($"Header {HeaderConstants.UploadOffset} must be a positive number");
            }

            return TaskHelper.Completed;
        }
    }
}
