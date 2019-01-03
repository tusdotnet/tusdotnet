using System.Linq;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace tusdotnet.Validation.Requirements
{
    internal sealed class UploadChecksum : Requirement
    {
        private Checksum RequestChecksum { get; }

        public UploadChecksum() : this(null)
        {
        }

        public UploadChecksum(Checksum requestChecksum)
        {
            RequestChecksum = requestChecksum;
        }

        public override async Task Validate(ContextAdapter context)
        {
            var providedChecksum = RequestChecksum ?? GetProvidedChecksum(context);

            if (context.Configuration.Store is ITusChecksumStore checksumStore && providedChecksum != null)
            {
                if (!providedChecksum.IsValid)
                {
                    await BadRequest($"Could not parse {HeaderConstants.UploadChecksum} header");
                    return;
                }

                var checksumAlgorithms = (await checksumStore.GetSupportedAlgorithmsAsync(context.CancellationToken)).ToList();
                if (!checksumAlgorithms.Contains(providedChecksum.Algorithm))
                {
                    await BadRequest(
                        $"Unsupported checksum algorithm. Supported algorithms are: {string.Join(",", checksumAlgorithms)}");
                }
            }
        }

        private static Checksum GetProvidedChecksum(ContextAdapter context)
        {
            return context.Request.Headers.ContainsKey(HeaderConstants.UploadChecksum)
                ? new Checksum(context.Request.Headers[HeaderConstants.UploadChecksum][0])
                : null;
        }
    }
}
