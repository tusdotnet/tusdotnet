using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;

namespace tusdotnet.Validation.Specifications
{
    internal class UploadOffset : Specification
    {
        public override Task Validate(ContextAdapter context)
        {
            if (!context.Request.Headers.ContainsKey(HeaderConstants.UploadOffset))
            {
                return BadRequest($"Missing {HeaderConstants.UploadOffset} header");
            }

            if (!long.TryParse(context.Request.Headers[HeaderConstants.UploadOffset].FirstOrDefault(), out long requestOffset))
            {
                return BadRequest($"Could not parse {HeaderConstants.UploadOffset} header");
            }

            if (requestOffset < 0)
            {
                return BadRequest($"Header {HeaderConstants.UploadOffset} must be a positive number");
            }

            return Done;
        }
    }
}
