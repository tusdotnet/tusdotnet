using System.Net;
using System.Threading.Tasks;
using tusdotnet.Adapters;
using tusdotnet.Constants;
using tusdotnet.Extensions;
using tusdotnet.Interfaces;
using tusdotnet.Validation;

namespace tusdotnet.ProtocolHandlers
{
    /*
    * An OPTIONS request MAY be used to gather information about the Server’s current configuration. 
    * A successful response indicated by the 204 No Content status MUST contain the Tus-Version header. 
    * It MAY include the Tus-Extension and Tus-Max-Size headers.
    * The Client SHOULD NOT include the Tus-Resumable header in the request and the Server MUST discard it.
    * */
    internal class OptionsHandler : ProtocolMethodHandler
    {
        internal override bool RequiresLock => false;

        internal override Requirement[] Requires => null;

        internal override bool CanHandleRequest(ContextAdapter context)
        {
            return context.UrlMatchesUrlPath();
        }

        internal override async Task<bool> Handle(ContextAdapter context)
        {
            var response = context.Response;

            response.SetHeader(HeaderConstants.TusResumable, HeaderConstants.TusResumableValue);
            response.SetHeader(HeaderConstants.TusVersion, HeaderConstants.TusResumableValue);

            var maximumAllowedSize = context.Configuration.GetMaxAllowedUploadSizeInBytes();

            if (maximumAllowedSize.HasValue)
            {
                response.SetHeader(HeaderConstants.TusMaxSize, maximumAllowedSize.Value.ToString());
            }

            var extensions = context.DetectExtensions();
            if (extensions.Count > 0)
            {
                response.SetHeader(HeaderConstants.TusExtension, string.Join(",", extensions));
            }

            if (context.Configuration.Store is ITusChecksumStore checksumStore)
            {
                var checksumAlgorithms = await checksumStore.GetSupportedAlgorithmsAsync(context.CancellationToken);
                response.SetHeader(HeaderConstants.TusChecksumAlgorithm, string.Join(",", checksumAlgorithms));
            }

            response.SetStatus(HttpStatusCode.NoContent);
            return true;
        }
    }
}