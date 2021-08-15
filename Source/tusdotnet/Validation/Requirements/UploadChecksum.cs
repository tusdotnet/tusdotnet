using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Helpers;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadChecksum : Requirement
    {
        private ChecksumHelper ChecksumHelper { get; }

        public UploadChecksum(ChecksumHelper checksumHelper)
        {
            ChecksumHelper = checksumHelper;
        }

        public override async Task Validate(ContextAdapter context)
        {
            if (!ChecksumHelper.IsSupported())
            {
                return;
            }

            var leadingHeaderResult = await ChecksumHelper.VerifyLeadingHeader();

            if (leadingHeaderResult.IsFailure())
            {
                await Error(leadingHeaderResult.Status, leadingHeaderResult.ErrorMessage);
                return;
            }

            var trailingHeaderResult = ChecksumHelper.VerifyStateForChecksumTrailer();

            if (trailingHeaderResult.IsFailure())
            {
                await Error(trailingHeaderResult.Status, trailingHeaderResult.ErrorMessage);
                return;
            }
        }
    }
}
