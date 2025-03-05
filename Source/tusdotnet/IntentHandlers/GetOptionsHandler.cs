using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Models;
using tusdotnet.Validation;

namespace tusdotnet.IntentHandlers
{
    /*
    * An OPTIONS request MAY be used to gather information about the Server’s current configuration.
    * A successful response indicated by the 204 No Content status MUST contain the Tus-Version header.
    * It MAY include the Tus-Extension and Tus-Max-Size headers.
    * The Client SHOULD NOT include the Tus-Resumable header in the request and the Server MUST discard it.
    */

    internal class GetOptionsHandler : IntentHandler
    {
        internal override Requirement[] Requires => NoRequirements;

        public GetOptionsHandler(ContextAdapter context)
            : base(context, IntentType.GetOptions, LockType.NoLock) { }

        internal override async Task Invoke()
        {
            var response = Context.Response;

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);

            var maximumAllowedSize = Context.Configuration.GetMaxAllowedUploadSizeInBytes();

            if (maximumAllowedSize.HasValue)
            {
                response.SetHeader(HeaderConstants.TusMaxSize, maximumAllowedSize.Value.ToString());
            }

            var extensions = Context.StoreAdapter.Extensions.ToList();
            if (extensions.Count > 0)
            {
                response.SetHeader(HeaderConstants.TusExtension, string.Join(",", extensions));
            }

            if (Context.StoreAdapter.Extensions.Checksum)
            {
                var checksumAlgorithms = await Context.StoreAdapter.GetSupportedAlgorithmsAsync(
                    Context.CancellationToken
                );
                response.SetHeader(
                    HeaderConstants.TusChecksumAlgorithm,
                    string.Join(",", checksumAlgorithms)
                );
            }

            response.SetResponse(HttpStatusCode.NoContent);
        }
    }
}
